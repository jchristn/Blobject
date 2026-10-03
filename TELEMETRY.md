# Blobject Telemetry

Blobject emits metrics and traces for every storage operation, bulk operation, connection, and `BlobCopy` job. It uses only the .NET base class library (`System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`) and never exports anything itself. Your application decides where the data goes (Prometheus, Tempo, Grafana Cloud, or any OTLP backend) by subscribing to two names:

| Signal  | Name       | Constant                                   |
|---------|------------|--------------------------------------------|
| Metrics | `Blobject` | `BlobjectTelemetryNames.MeterName`          |
| Traces  | `Blobject` | `BlobjectTelemetryNames.ActivitySourceName` |

Telemetry is available from `Blobject.Core` 6.1.0 and every provider package at 6.1.0. It is on by default and costs a few checks per operation when nothing subscribes. Instrumentation is best-effort: a failure while recording telemetry, including an exception thrown by your own listener, never affects a storage operation.

Blobject is a library, so it has no logs pipeline, Docker Compose stack, or Grafana provisioning of its own. Those belong to the host application. The PromQL below is ready to paste into the host's dashboards and alert rules.

## Contents

- [Subscribing](#subscribing)
- [Configuration](#configuration)
- [What an operator can answer](#what-an-operator-can-answer)
- [Metrics catalog](#metrics-catalog)
- [Label values](#label-values)
- [Spans catalog](#spans-catalog)
- [Recommended alerts](#recommended-alerts)
- [Dashboard map](#dashboard-map)
- [Implementing a custom provider](#implementing-a-custom-provider)
- [Privacy and cardinality](#privacy-and-cardinality)

## Subscribing

### Radiant

```csharp
RadiantSettings settings = new RadiantSettings("my-service");
settings.Otlp.Endpoint = "http://127.0.0.1:4317";
settings.Sources.AddMeter("Blobject");
settings.Sources.AddActivitySource("Blobject");

using (RadiantHost host = RadiantHost.Start(settings))
{
    // run the application
}
```

### OpenTelemetry .NET SDK

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(BlobjectTelemetryNames.MeterName)
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddSource(BlobjectTelemetryNames.ActivitySourceName)
        .AddOtlpExporter());
```

### Without an SDK

A `MeterListener` and an `ActivityListener` filtered to `"Blobject"` receive everything. The test suite's `src/Test.Shared/Telemetry/TelemetryCollector.cs` is a complete example.

### Trace context

Blobject spans are children of whatever `Activity.Current` is when you call Blobject. Inside an ASP.NET Core or Watson request handler, storage calls nest under the request span with no extra code. W3C context also flows into the background work Blobject starts: `WriteManyAsync`, `DeleteManyAsync`, and `EmptyAsync` run items on the thread pool, and each item's span is still a child of the bulk operation's span.

Blobject talks to storage through the AWS, Azure, Google, OpenCIFS, OpenNFS, and S3Lite SDKs. Any outbound HTTP spans or `traceparent` headers those SDKs produce come from the SDKs and from `System.Net.Http` instrumentation in your host, not from Blobject.

From 6.1.1, `Blobject.NFS` uses OpenNFS.Client 0.2.0 and `Blobject.AmazonS3Lite` uses S3Lite 1.3.0, both of which emit their own metrics and traces. Subscribe to them alongside `Blobject` to see protocol-level detail beneath each Blobject span: the SDK spans are children of the `nfs <operation>` or `aws_s3_lite <operation>` span that issued them (or of `nfs connect` when a connection is being established).

| SDK            | Meter            | ActivitySource   |
|----------------|------------------|------------------|
| OpenNFS.Client | `OpenNFS.Client` | `OpenNFS.Client` |
| S3Lite         | `S3Lite`         | `S3Lite`         |

## Configuration

All settings are static properties on `Blobject.Core.BlobjectTelemetry`:

| Property     | Default | Effect |
|--------------|---------|--------|
| `Enabled`    | `true`  | Master switch. When `false`, no spans are created and no metrics are recorded. |
| `RecordKeys` | `false` | Adds the object key as the `blobject.key` span attribute. Keys can contain user data such as file names, so they are omitted unless you opt in. Keys never appear on metrics. |
| `Version`    | (read-only) | The `Blobject.Core` version reported by `blobject.build.info`. |

Histogram bucket boundaries are a collector concern. Durations are recorded in seconds, so the OpenTelemetry default boundaries work. For object stores with long tails, consider adding `10`, `30`, and `60` second buckets in your SDK's view configuration.

## What an operator can answer

| Question | Where to look |
|----------|---------------|
| Is storage slow, and which provider or operation? | `blobject_operation_duration_seconds` p95 by `blobject_provider`, `blobject_operation` |
| What is failing, and how? | `blobject_operation_errors_total` by `blobject_operation`, `error_type`. Use `blobject_outcome="not_found"` to tell missing objects from real failures. |
| Why was this one request slow? | The trace: the request span, then the `<provider> <operation>` span, then `<provider> connect` if a connection had to be (re)established. |
| Is a bulk job starved of concurrency? | `blobject_bulk_queue_duration_seconds` (time each item waited for a slot) against `blobject_bulk_slots_in_use` and `blobject_bulk_slots_capacity` |
| Is the file server flapping? | `blobject_connection_resets_total`, `blobject_operation_retries_total`, `blobject_connection_duration_seconds{blobject_outcome="error"}` |
| Did the copy job run, and which stage is slow? | `blobject_copy_jobs_total`, `blobject_copy_last_success_seconds`, `blobject_copy_stage_duration_seconds` by `blobject_stage` |
| How much data moves? | `blobject_io_bytes_total` by `blobject_direction`, `blobject_copy_bytes_total` |

## Metrics catalog

Prometheus names assume the standard OpenTelemetry Prometheus exporter, which converts dots to underscores, appends the unit (`_seconds`, `_bytes`), and appends `_total` to counters. Label keys convert the same way (`blobject.provider` becomes `blobject_provider`).

### Operations

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `blobject.operation.duration` | `blobject_operation_duration_seconds` | Histogram | s | `blobject.provider`, `blobject.operation`, `blobject.outcome`, `error.type` (failures only) | Duration of every storage operation. `_count` is the operation counter. |
| `blobject.operation.active` | `blobject_operation_active` | UpDownCounter | {operation} | `blobject.provider`, `blobject.operation` | Operations in flight. |
| `blobject.operation.errors` | `blobject_operation_errors_total` | Counter | {error} | `blobject.provider`, `blobject.operation`, `blobject.outcome`, `error.type` | Failed operations (`error` and `not_found`; cancellations are excluded). |
| `blobject.operation.retries` | `blobject_operation_retries_total` | Counter | {retry} | `blobject.provider`, `blobject.operation` | Operation attempts retried after a transport failure (CIFS, NFS). |
| `blobject.io.bytes` | `blobject_io_bytes_total` | Counter | By | `blobject.provider`, `blobject.operation`, `blobject.direction` | Object bytes read (`get`, `get_stream`) or written (`write`) by successful operations. For `get_stream` this is the object's declared length when the stream is opened. |
| `blobject.enumerate.objects` | `blobject_enumerate_objects_total` | Counter | {object} | `blobject.provider` | Objects returned by enumeration. |

### Bulk operations (`write_many`, `delete_many`, `empty`)

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `blobject.bulk.items` | `blobject_bulk_items_total` | Counter | {item} | `blobject.provider`, `blobject.operation`, `blobject.outcome` (`success`, `error`) | Items processed. `DeleteManyAsync` reports per-key results, including native S3 and Azure batch deletes. |
| `blobject.bulk.queue.duration` | `blobject_bulk_queue_duration_seconds` | Histogram | s | `blobject.provider`, `blobject.operation` | The queued stage: how long each item waited for one of `MaxConcurrency` slots. |
| `blobject.bulk.slots.in_use` | `blobject_bulk_slots_in_use` | UpDownCounter | {slot} | `blobject.provider`, `blobject.operation` | Concurrency slots in use. |
| `blobject.bulk.slots.capacity` | `blobject_bulk_slots_capacity` | UpDownCounter | {slot} | `blobject.provider`, `blobject.operation` | Slots available to running bulk operations (the sum of their `MaxConcurrency`). |

### Connections (CIFS, NFS)

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `blobject.connection.duration` | `blobject_connection_duration_seconds` | Histogram | s | `blobject.provider`, `blobject.outcome`, `error.type` (failures only) | Time to connect, authenticate, and open the share or mount the export. |
| `blobject.connection.open` | `blobject_connection_open` | UpDownCounter | {connection} | `blobject.provider` | Open connections. |
| `blobject.connection.pool.capacity` | `blobject_connection_pool_capacity` | UpDownCounter | {connection} | `blobject.provider` | Pool capacity of live clients: `CifsSettings.MaxConnections` per CIFS client, 1 per NFS client. Released on dispose. |
| `blobject.connection.resets` | `blobject_connection_resets_total` | Counter | {reset} | `blobject.provider`, `error.type` | Connections discarded because they failed mid-operation (the exception type), or were found disconnected before use, for example after a server restart (`connection_lost`). |

### BlobCopy pipeline

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `blobject.copy.jobs` | `blobject_copy_jobs_total` | Counter | {job} | `blobject.copy.source`, `blobject.copy.target`, `blobject.outcome`, `error.type` (failures only) | Copy jobs by outcome. |
| `blobject.copy.duration` | `blobject_copy_duration_seconds` | Histogram | s | `blobject.copy.source`, `blobject.copy.target`, `blobject.outcome`, `error.type` (failures only) | Copy job duration. |
| `blobject.copy.stage.duration` | `blobject_copy_stage_duration_seconds` | Histogram | s | `blobject.copy.source`, `blobject.copy.target`, `blobject.stage`, `blobject.outcome` | Per-stage duration: `enumerate` (fetching the next source object), `read` (opening the source stream), `write` (writing the target). |
| `blobject.copy.stage.events` | `blobject_copy_stage_events_total` | Counter | {event} | `blobject.copy.source`, `blobject.copy.target`, `blobject.stage`, `blobject.outcome` | Per-stage events by outcome. |
| `blobject.copy.objects` | `blobject_copy_objects_total` | Counter | {object} | `blobject.copy.source`, `blobject.copy.target` | Objects copied. |
| `blobject.copy.bytes` | `blobject_copy_bytes_total` | Counter | By | `blobject.copy.source`, `blobject.copy.target` | Bytes copied. |
| `blobject.copy.last_success` | `blobject_copy_last_success_seconds` | Gauge | s | `blobject.copy.source`, `blobject.copy.target` | Unix time of the last successful copy job in this process. |

`BlobCopy` processes objects one at a time, so it has no `queued` stage. Bulk operations report their queued stage as `blobject.bulk.queue.duration`.

### Build information

| Instrument | Prometheus | Type | Labels | Description |
|---|---|---|---|---|
| `blobject.build.info` | `blobject_build_info` | Gauge | `blobject.version` | Always 1. Registered when Blobject is first used. |

Runtime metrics (GC, thread pool, CPU) are the host's responsibility. Radiant ships them by default; with the OpenTelemetry SDK, add `AddRuntimeInstrumentation()`.

## Label values

All metric labels are bounded:

| Label | Values |
|---|---|
| `blobject.provider`, `blobject.copy.source`, `blobject.copy.target` | `aws_s3`, `aws_s3_lite`, `azure_blob`, `gcp_storage`, `disk`, `cifs`, `nfs`, `custom` |
| `blobject.operation` | `validate_connectivity`, `list_containers`, `get`, `get_stream`, `get_metadata`, `write`, `write_many`, `delete`, `delete_many`, `delete_batch`, `exists`, `enumerate`, `empty` |
| `blobject.outcome` | `success`, `not_found`, `cancelled`, `error` |
| `blobject.direction` | `read`, `write` |
| `blobject.stage` | `enumerate`, `read`, `write` |
| `error.type` | Full .NET exception type name, for example `System.IO.IOException` or `Amazon.S3.AmazonS3Exception`, or `connection_lost` |

`not_found` is reported when the object does not exist: `KeyNotFoundException`, `FileNotFoundException`, `DirectoryNotFoundException`, an S3 404 or `NoSuchKey`, an Azure 404, or a Google Cloud 404. `cancelled` is reported for `OperationCanceledException`; cancelled spans keep status `Unset` and are not counted as errors.

`GenerateUrl` builds a string locally without contacting storage, so it is not instrumented.

## Spans catalog

| Span name | Kind | When | Attributes |
|---|---|---|---|
| `<provider> <operation>`, for example `aws_s3 get` or `cifs write` | Client (`Internal` for `disk`) | Every public storage operation | `blobject.provider`, `blobject.operation`, `blobject.outcome`, `blobject.container`, `server.address` (where known), `blobject.key` (only with `RecordKeys`), `blobject.bytes`, `error.type` (failures) |
| `<provider> enumerate` | Client | `Enumerate` and `EnumerateAsync`, from the first item to completion or abandonment | Adds `blobject.objects` |
| `<provider> write_many`, `delete_many`, `empty` | Client | Bulk operations; item spans are children | Adds `blobject.items`, `blobject.items.failed`, `blobject.max_concurrency` |
| `<provider> delete_batch` | Client | Each native batch request inside `DeleteManyAsync` (S3 up to 1000 keys, Azure up to 256) | Same as operations |
| `<provider> connect` | Client | A CIFS or NFS connection being established, as a child of the operation that needed it | `blobject.provider`, `blobject.container`, `server.address`, `blobject.outcome`, `error.type` |
| `blobject copy` | Internal | A `BlobCopy.StartAsync` job | `blobject.copy.source`, `blobject.copy.target`, `blobject.outcome`, `blobject.objects`, `blobject.bytes`, `error.type` |
| `stage:read`, `stage:write` | Internal | Each object's read and write stage in a copy job, under `blobject copy` and wrapping the provider span | `blobject.copy.source`, `blobject.copy.target`, `blobject.stage` |

Span status is set explicitly: `Ok` on success, `Error` with the exception message on failure (plus an OpenTelemetry `exception` event carrying `exception.type`, `exception.message`, and `exception.stacktrace`), and `Unset` on cancellation. When a connection is reset or an attempt is retried during an operation, the operation span gets a `blobject.connection.reset` or `blobject.retry` event.

When one overload delegates to another (for example `WriteAsync(string)` calling `WriteAsync(byte[])`), the work is recorded once.

## Recommended alerts

```yaml
groups:
  - name: blobject
    rules:
      - alert: BlobjectErrorRatioHigh
        expr: |
          sum by (blobject_provider, blobject_operation) (rate(blobject_operation_errors_total{blobject_outcome="error"}[5m]))
            /
          sum by (blobject_provider, blobject_operation) (rate(blobject_operation_duration_seconds_count[5m]))
            > 0.05
        for: 10m
        labels: { severity: warning }
        annotations:
          summary: "Blobject {{ $labels.blobject_provider }} {{ $labels.blobject_operation }} failing above 5%"

      - alert: BlobjectLatencyHigh
        expr: |
          histogram_quantile(0.95, sum by (le, blobject_provider, blobject_operation)
            (rate(blobject_operation_duration_seconds_bucket{blobject_operation!~"enumerate|empty|write_many|delete_many"}[5m]))) > 2
        for: 10m
        labels: { severity: warning }
        annotations:
          summary: "Blobject {{ $labels.blobject_provider }} {{ $labels.blobject_operation }} p95 above 2s"

      - alert: BlobjectConnectionFlapping
        expr: sum by (blobject_provider) (increase(blobject_connection_resets_total[10m])) > 5
        labels: { severity: warning }
        annotations:
          summary: "Blobject {{ $labels.blobject_provider }} connections are being reset repeatedly"

      - alert: BlobjectConnectFailing
        expr: sum by (blobject_provider) (rate(blobject_connection_duration_seconds_count{blobject_outcome="error"}[5m])) > 0
        for: 5m
        labels: { severity: critical }
        annotations:
          summary: "Blobject cannot connect to {{ $labels.blobject_provider }}"

      - alert: BlobjectBulkQueueSaturated
        expr: |
          histogram_quantile(0.95, sum by (le, blobject_provider, blobject_operation)
            (rate(blobject_bulk_queue_duration_seconds_bucket[5m]))) > 1
        for: 15m
        labels: { severity: info }
        annotations:
          summary: "Bulk {{ $labels.blobject_operation }} items wait over 1s for a slot; consider raising MaxConcurrency"

      - alert: BlobjectCopyStale
        # adjust 86400 to your copy schedule
        expr: time() - max by (blobject_copy_source, blobject_copy_target) (blobject_copy_last_success_seconds) > 86400
        labels: { severity: warning }
        annotations:
          summary: "No successful copy from {{ $labels.blobject_copy_source }} to {{ $labels.blobject_copy_target }} in 24h"

      - alert: BlobjectCopyFailed
        expr: increase(blobject_copy_jobs_total{blobject_outcome="error"}[1h]) > 0
        labels: { severity: warning }
        annotations:
          summary: "A Blobject copy job failed"
```

Missing objects (`not_found`) are excluded from the error-ratio alert on purpose, because many applications probe for objects that may not exist. Alert on them separately if a missing object is unexpected in your workload.

## Dashboard map

Blobject does not ship a Grafana stack, because it is a library. In a host service's product folder, add a **Storage** dashboard (or a row in an Integrations dashboard) with these panels:

| Panel | Query |
|---|---|
| Operation rate by outcome | `sum by (blobject_provider, blobject_operation, blobject_outcome) (rate(blobject_operation_duration_seconds_count[$__rate_interval]))` |
| p95 latency by operation | `histogram_quantile(0.95, sum by (le, blobject_provider, blobject_operation) (rate(blobject_operation_duration_seconds_bucket[$__rate_interval])))` |
| Errors by type | `sum by (blobject_provider, blobject_operation, error_type) (rate(blobject_operation_errors_total[$__rate_interval]))` |
| In flight | `sum by (blobject_provider, blobject_operation) (blobject_operation_active)` |
| Throughput | `sum by (blobject_provider, blobject_direction) (rate(blobject_io_bytes_total[$__rate_interval]))` |
| Bulk queue wait p95 | `histogram_quantile(0.95, sum by (le, blobject_operation) (rate(blobject_bulk_queue_duration_seconds_bucket[$__rate_interval])))` |
| Bulk slots | `sum by (blobject_operation) (blobject_bulk_slots_in_use)` and `sum by (blobject_operation) (blobject_bulk_slots_capacity)` |
| Bulk item failures | `sum by (blobject_operation) (rate(blobject_bulk_items_total{blobject_outcome="error"}[$__rate_interval]))` |
| Connections | `sum by (blobject_provider) (blobject_connection_open)` and `sum by (blobject_provider) (blobject_connection_pool_capacity)` |
| Resets and retries | `sum by (blobject_provider, error_type) (rate(blobject_connection_resets_total[$__rate_interval]))`, `sum by (blobject_provider) (rate(blobject_operation_retries_total[$__rate_interval]))` |
| Copy jobs | `sum by (blobject_outcome) (increase(blobject_copy_jobs_total[$__range]))` |
| Copy stage p95 | `histogram_quantile(0.95, sum by (le, blobject_stage) (rate(blobject_copy_stage_duration_seconds_bucket[$__rate_interval])))` |
| Time since last copy | `time() - max(blobject_copy_last_success_seconds)` |
| Library version | `blobject_build_info` |

For traces in Tempo, search `{ resource.service.name = "<your service>" && span.blobject.provider != nil }`, or `{ span.blobject.outcome = "error" }` to find failed storage calls.

## Implementing a custom provider

A client derived from `BlobClientBase` outside this repository still gets bulk-operation telemetry (`WriteManyAsync`, `DeleteManyAsync`, `EmptyAsync`) from the base class, reported as provider `custom`. To instrument its own operations, use the protected helpers:

```csharp
protected override string TelemetryProvider { get { return "my_store"; } } // keep it a small fixed value
protected override string TelemetryContainer { get { return _Bucket; } }

public override Task<byte[]> GetAsync(string key, CancellationToken token = default)
{
    return InstrumentAsync(BlobjectTelemetryNames.OperationGet, key, async () =>
    {
        byte[] data = await DownloadAsync(key, token).ConfigureAwait(false);
        SetTelemetryBytes(data.Length);
        return data;
    });
}

public override IAsyncEnumerable<BlobMetadata> EnumerateAsync(EnumerationFilter filter = null, CancellationToken token = default)
{
    return InstrumentEnumerateAsync(t => ListAsync(filter, t), token);
}
```

The full set: `InstrumentAsync`, `InstrumentEnumerate`, `InstrumentEnumerateAsync`, `InstrumentConnectAsync`, `SetTelemetryBytes`, `SetTelemetryItems`, `RecordTelemetryItem`, `RecordTelemetryConnectionClosed`, `RecordTelemetryConnectionReset`, `RecordTelemetryRetry`, `RecordTelemetryPoolCapacity`, `ForEachConcurrentAsync`, and the overridable `TelemetryProvider`, `TelemetryContainer`, `TelemetryServerAddress`, and `IsTelemetryNotFound`.

## Privacy and cardinality

- Metric labels contain only the bounded values listed above. Object keys, bucket and container names, host names, and exception messages are never metric labels.
- Spans carry the container (bucket, container, share, export, or directory) and the server host name, never credentials, access keys, SAS tokens, or object contents.
- Object keys appear on spans only when `BlobjectTelemetry.RecordKeys = true`.
- Exception messages appear on failed spans (status description and the `exception` event) because they are what tells an operator what failed. Some messages include the object key, for example `Could not find file 'path/to/key'`. If keys are sensitive in your environment, filter `exception.message` in your collector.
