namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.NFS;
    using Touchstone.Core;

    /// <summary>
    /// Server-independent API surface tests for the CIFS and NFS providers: settings validation and defaults,
    /// constructor validation, URL generation, argument validation ahead of any I/O, serialization, and a
    /// snapshot of the public API.
    /// </summary>
    public static class FileShareApiSuites
    {
        #region Public-Methods

        /// <summary>
        /// Build the suites.
        /// </summary>
        /// <returns>Suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> Build()
        {
            return new List<TestSuiteDescriptor>
            {
                new TestSuiteDescriptor("CifsApi", "CIFS API surface", new List<TestCaseDescriptor>
                {
                    Case("CifsApi", "SettingsDefaults", "CifsSettings defaults", CifsSettingsDefaults),
                    Case("CifsApi", "SettingsConstructorValidation", "CifsSettings constructors validate arguments", CifsSettingsConstructorValidation),
                    Case("CifsApi", "SettingsHostnameSetter", "setting CifsSettings.Hostname updates Hostname and Ip", CifsSettingsHostnameSetter),
                    Case("CifsApi", "SettingsPropertyValidation", "CifsSettings property setters validate values", CifsSettingsPropertyValidation),
                    Case("CifsApi", "SettingsShareNormalization", "CifsSettings.Share normalizes separators", CifsSettingsShareNormalization),
                    Case("CifsApi", "ClientConstructorValidation", "CifsBlobClient validates settings without connecting", CifsClientConstructorValidation),
                    Case("CifsApi", "GenerateUrl", "CifsBlobClient.GenerateUrl builds UNC paths", CifsGenerateUrl),
                    Case("CifsApi", "ArgumentValidationBeforeIo", "invalid keys are rejected before connecting", CifsArgumentValidationBeforeIo),
                    Case("CifsApi", "ClientInterfaces", "CifsBlobClient implements IDisposable and IAsyncDisposable", CifsClientInterfaces),
                    Case("CifsApi", "PublicSurface", "CIFS public API matches the documented surface", CifsPublicSurface),
                    Case("CifsApi", "Dependencies", "Blobject.CIFS depends on OpenCIFS and not EzSmb", CifsDependencies)
                }),
                new TestSuiteDescriptor("NfsApi", "NFS API surface", new List<TestCaseDescriptor>
                {
                    Case("NfsApi", "SettingsDefaults", "NfsSettings defaults", NfsSettingsDefaults),
                    Case("NfsApi", "SettingsConstructorValidation", "NfsSettings constructors validate arguments", NfsSettingsConstructorValidation),
                    Case("NfsApi", "SettingsHostnameSetter", "setting NfsSettings.Hostname updates Hostname and Ip", NfsSettingsHostnameSetter),
                    Case("NfsApi", "SettingsPropertyValidation", "NfsSettings property setters validate values", NfsSettingsPropertyValidation),
                    Case("NfsApi", "SettingsShareNormalization", "NfsSettings.Share normalizes separators", NfsSettingsShareNormalization),
                    Case("NfsApi", "ClientConstructorValidation", "NfsBlobClient validates settings and version without connecting", NfsClientConstructorValidation),
                    Case("NfsApi", "GenerateUrl", "NfsBlobClient.GenerateUrl builds RFC 2224 URLs", NfsGenerateUrl),
                    Case("NfsApi", "ArgumentValidationBeforeIo", "invalid keys are rejected before connecting", NfsArgumentValidationBeforeIo),
                    Case("NfsApi", "EnumSerialization", "NFS enums serialize as strings", NfsEnumSerialization),
                    Case("NfsApi", "ClientInterfaces", "NfsBlobClient implements IDisposable and IAsyncDisposable", NfsClientInterfaces),
                    Case("NfsApi", "PublicSurface", "NFS public API matches the documented surface", NfsPublicSurface),
                    Case("NfsApi", "Dependencies", "Blobject.NFS depends on OpenNFS and not NFS-Client", NfsDependencies)
                })
            };
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, execute, new List<string> { "fileshare", "api" });
        }

        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Action execute)
        {
            return Case(suiteId, caseId, displayName, ct =>
            {
                execute();
                return Task.CompletedTask;
            });
        }

        private static CifsSettings ValidCifs()
        {
            return new CifsSettings("127.0.0.1", "user", "pass", "share");
        }

        private static NfsSettings ValidNfs()
        {
            return new NfsSettings("127.0.0.1", 0, 0, "/export", NfsVersionEnum.V3);
        }

        #endregion

        #region Cifs

        private static void CifsSettingsDefaults()
        {
            CifsSettings s = new CifsSettings();
            Check.Equal("localhost", s.Hostname, "Hostname");
            Check.Equal(IPAddress.Parse("127.0.0.1"), s.Ip, "Ip");
            Check.Equal(445, s.Port, "Port");
            Check.Equal("", s.Domain, "Domain");
            Check.False(s.RequireSigning, "RequireSigning");
            Check.True(s.PreferEncryption, "PreferEncryption");
            Check.Equal(10000, s.ConnectTimeoutMs, "ConnectTimeoutMs");
            Check.Equal(4, s.MaxConnections, "MaxConnections");
            Check.Null(s.Username, "Username");
            Check.Null(s.Password, "Password");
            Check.Null(s.Share, "Share");

            CifsSettings full = new CifsSettings("127.0.0.1", 4450, "user", "pass", "share");
            Check.Equal(4450, full.Port, "Port from constructor");
            Check.Equal("user", full.Username, "Username from constructor");
            Check.Equal("pass", full.Password, "Password from constructor");
            Check.Equal("share", full.Share, "Share from constructor");
            Check.Equal("127.0.0.1", full.Hostname, "Hostname from constructor");
            Check.True(full is BlobSettings, "derives from BlobSettings");
        }

        private static void CifsSettingsConstructorValidation()
        {
            Check.Throws<ArgumentNullException>(() => new CifsSettings(null, "u", "p", "s"), "null hostname");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("", "u", "p", "s"), "empty hostname");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("127.0.0.1", null, "p", "s"), "null user");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("127.0.0.1", "", "p", "s"), "empty user");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("127.0.0.1", "u", null, "s"), "null password");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("127.0.0.1", "u", "", "s"), "empty password");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("127.0.0.1", "u", "p", null), "null share");
            Check.Throws<ArgumentNullException>(() => new CifsSettings("127.0.0.1", "u", "p", ""), "empty share");
            Check.Throws<ArgumentException>(() => new CifsSettings("127.0.0.1", "u", "p", "/"), "separator-only share");
            Check.Throws<ArgumentException>(() => new CifsSettings("no-such-host.invalid", "u", "p", "s"), "unresolvable hostname");
            Check.Throws<ArgumentOutOfRangeException>(() => new CifsSettings("127.0.0.1", 0, "u", "p", "s"), "port 0");
            Check.Throws<ArgumentOutOfRangeException>(() => new CifsSettings("127.0.0.1", 65536, "u", "p", "s"), "port 65536");
        }

        private static void CifsSettingsHostnameSetter()
        {
            CifsSettings s = ValidCifs();
            s.Hostname = "127.0.0.2";
            Check.Equal("127.0.0.2", s.Hostname, "Hostname after set");
            Check.Equal(IPAddress.Parse("127.0.0.2"), s.Ip, "Ip after set");

            s.Hostname = "localhost";
            Check.Equal("localhost", s.Hostname, "Hostname after named set");
            Check.NotNull(s.Ip, "Ip after named set");

            Check.Throws<ArgumentNullException>(() => s.Hostname = null, "null hostname");
            Check.Throws<ArgumentNullException>(() => s.Hostname = "", "empty hostname");
            Check.Throws<ArgumentException>(() => s.Hostname = "no-such-host.invalid", "unresolvable hostname");
            Check.Equal("localhost", s.Hostname, "Hostname unchanged after failed set");
        }

        private static void CifsSettingsPropertyValidation()
        {
            CifsSettings s = ValidCifs();
            Check.Throws<ArgumentOutOfRangeException>(() => s.Port = 0, "Port 0");
            Check.Throws<ArgumentOutOfRangeException>(() => s.Port = 65536, "Port 65536");
            s.Port = 1;
            s.Port = 65535;
            Check.Equal(65535, s.Port, "Port max");

            Check.Throws<ArgumentOutOfRangeException>(() => s.ConnectTimeoutMs = 0, "ConnectTimeoutMs 0");
            s.ConnectTimeoutMs = 1;
            Check.Equal(1, s.ConnectTimeoutMs, "ConnectTimeoutMs min");

            Check.Throws<ArgumentOutOfRangeException>(() => s.MaxConnections = 0, "MaxConnections 0");
            Check.Throws<ArgumentOutOfRangeException>(() => s.MaxConnections = 65, "MaxConnections 65");
            s.MaxConnections = 1;
            s.MaxConnections = 64;
            Check.Equal(64, s.MaxConnections, "MaxConnections max");

            Check.Throws<ArgumentNullException>(() => s.Username = null, "null Username");
            Check.Throws<ArgumentNullException>(() => s.Password = "", "empty Password");
            Check.Throws<ArgumentNullException>(() => s.Share = null, "null Share");

            s.Domain = null;
            Check.Equal("", s.Domain, "null Domain becomes empty");
            s.Domain = "CONTOSO";
            Check.Equal("CONTOSO", s.Domain, "Domain");

            s.RequireSigning = true;
            s.PreferEncryption = false;
            Check.True(s.RequireSigning, "RequireSigning set");
            Check.False(s.PreferEncryption, "PreferEncryption set");
        }

        private static void CifsSettingsShareNormalization()
        {
            CifsSettings s = ValidCifs();
            s.Share = "share/";
            Check.Equal("share", s.Share, "trailing slash");
            s.Share = "\\share\\";
            Check.Equal("share", s.Share, "leading and trailing backslash");
            s.Share = "share///";
            Check.Equal("share", s.Share, "repeated trailing slashes");
            Check.Throws<ArgumentException>(() => s.Share = "\\", "separator-only share");
        }

        private static void CifsClientConstructorValidation()
        {
            Check.Throws<ArgumentNullException>(() => new CifsBlobClient(null), "null settings");
            Check.Throws<ArgumentException>(() => new CifsBlobClient(new CifsSettings()), "settings without credentials");

            using (CifsBlobClient client = new CifsBlobClient(new CifsSettings("127.0.0.1", 1, "u", "p", "s")))
            {
                Check.Equal(4, client.MaxConcurrency, "default MaxConcurrency");
                Check.Equal(65536, client.StreamBufferSize, "default StreamBufferSize");
            }
        }

        private static void CifsGenerateUrl()
        {
            using (CifsBlobClient client = new CifsBlobClient(ValidCifs()))
            {
                Check.Equal("\\\\127.0.0.1\\share\\file.txt", client.GenerateUrl("file.txt"), "root file");
                Check.Equal("\\\\127.0.0.1\\share\\dir\\sub\\file.txt", client.GenerateUrl("dir/sub/file.txt"), "nested file");
                Check.Equal("\\\\127.0.0.1\\share\\", client.GenerateUrl(""), "empty key");
            }
        }

        private static async Task CifsArgumentValidationBeforeIo(CancellationToken token)
        {
            // port 1 is never an SMB server; any network attempt would fail with a connection error instead
            using (CifsBlobClient client = new CifsBlobClient(new CifsSettings("127.0.0.1", 1, "u", "p", "s") { ConnectTimeoutMs = 1000 }))
            {
                await AssertKeyValidation(client, token).ConfigureAwait(false);
            }
        }

        private static void CifsClientInterfaces()
        {
            Check.True(typeof(IDisposable).IsAssignableFrom(typeof(CifsBlobClient)), "IDisposable");
            Check.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(CifsBlobClient)), "IAsyncDisposable");
            Check.True(typeof(BlobClientBase).IsAssignableFrom(typeof(CifsBlobClient)), "BlobClientBase");
        }

        private static void CifsPublicSurface()
        {
            AssertSurface(typeof(CifsBlobClient), new[]
            {
                "C:(CifsSettings)",
                "M:DeleteAsync(String,CancellationToken):Task",
                "M:DeleteManyAsync(IEnumerable`1,CancellationToken):Task`1",
                "M:Dispose():Void",
                "M:DisposeAsync():ValueTask",
                "M:EmptyAsync(CancellationToken):Task`1",
                "M:Enumerate(EnumerationFilter):IEnumerable`1",
                "M:EnumerateAsync(EnumerationFilter,CancellationToken):IAsyncEnumerable`1",
                "M:ExistsAsync(String,CancellationToken):Task`1",
                "M:GenerateUrl(String,CancellationToken):String",
                "M:GetAsync(String,CancellationToken):Task`1",
                "M:GetMetadataAsync(String,CancellationToken):Task`1",
                "M:GetStreamAsync(String,CancellationToken):Task`1",
                "M:ListShares(CancellationToken):Task`1",
                "M:ValidateConnectivity(CancellationToken):Task`1",
                "M:WriteAsync(String,String,Byte[],CancellationToken):Task",
                "M:WriteAsync(String,String,Int64,Stream,CancellationToken):Task",
                "M:WriteAsync(String,String,String,CancellationToken):Task",
                "M:WriteManyAsync(List`1,CancellationToken):Task"
            });

            AssertSurface(typeof(CifsSettings), new[]
            {
                "C:()",
                "C:(String,Int32,String,String,String)",
                "C:(String,String,String,String)",
                "P:ConnectTimeoutMs:Int32:get,set",
                "P:Domain:String:get,set",
                "P:Hostname:String:get,set",
                "P:Ip:IPAddress:get",
                "P:MaxConnections:Int32:get,set",
                "P:Password:String:get,set",
                "P:Port:Int32:get,set",
                "P:PreferEncryption:Boolean:get,set",
                "P:RequireSigning:Boolean:get,set",
                "P:Share:String:get,set",
                "P:Username:String:get,set"
            });
        }

        private static void CifsDependencies()
        {
            AssemblyName[] references = typeof(CifsBlobClient).Assembly.GetReferencedAssemblies();
            Check.True(references.Any(r => r.Name == "OpenCIFS.Client"), "references OpenCIFS.Client");
            Check.False(references.Any(r => r.Name.StartsWith("EzSmb", StringComparison.OrdinalIgnoreCase)), "does not reference EzSmb");
        }

        #endregion

        #region Nfs

        private static void NfsSettingsDefaults()
        {
            NfsSettings s = new NfsSettings();
            Check.Equal("localhost", s.Hostname, "Hostname");
            Check.Equal(IPAddress.Parse("127.0.0.1"), s.Ip, "Ip");
            Check.Equal(0, s.UserId, "UserId");
            Check.Equal(0, s.GroupId, "GroupId");
            Check.Null(s.Share, "Share");
            Check.Equal(NfsVersionEnum.V3, s.Version, "Version");
            Check.Equal(2049, s.Port, "Port");
            Check.Equal(0, s.MountPort, "MountPort");
            Check.Equal(111, s.PortmapperPort, "PortmapperPort");
            Check.Equal(Environment.MachineName, s.MachineName, "MachineName");
            Check.Equal(NfsWriteStabilityEnum.Unstable, s.WriteStability, "WriteStability");
            Check.Equal(10000, s.ConnectTimeoutMs, "ConnectTimeoutMs");
            Check.Equal(30000, s.ResponseTimeoutMs, "ResponseTimeoutMs");

            NfsSettings full = new NfsSettings("127.0.0.1", 1000, 2000, "/export", NfsVersionEnum.V3);
            Check.Equal(1000, full.UserId, "UserId from constructor");
            Check.Equal(2000, full.GroupId, "GroupId from constructor");
            Check.Equal("/export", full.Share, "Share from constructor");
            Check.True(full is BlobSettings, "derives from BlobSettings");
        }

        private static void NfsSettingsConstructorValidation()
        {
            Check.Throws<ArgumentNullException>(() => new NfsSettings(null, 0, 0, "/e", NfsVersionEnum.V3), "null hostname");
            Check.Throws<ArgumentNullException>(() => new NfsSettings("", 0, 0, "/e", NfsVersionEnum.V3), "empty hostname");
            Check.Throws<ArgumentNullException>(() => new NfsSettings("127.0.0.1", 0, 0, null, NfsVersionEnum.V3), "null share");
            Check.Throws<ArgumentNullException>(() => new NfsSettings("127.0.0.1", 0, 0, "", NfsVersionEnum.V3), "empty share");
            Check.Throws<ArgumentOutOfRangeException>(() => new NfsSettings("127.0.0.1", -1, 0, "/e", NfsVersionEnum.V3), "negative user");
            Check.Throws<ArgumentOutOfRangeException>(() => new NfsSettings("127.0.0.1", 0, -1, "/e", NfsVersionEnum.V3), "negative group");
            Check.Throws<ArgumentException>(() => new NfsSettings("no-such-host.invalid", 0, 0, "/e", NfsVersionEnum.V3), "unresolvable hostname");
        }

        private static void NfsSettingsHostnameSetter()
        {
            NfsSettings s = ValidNfs();
            s.Hostname = "127.0.0.3";
            Check.Equal("127.0.0.3", s.Hostname, "Hostname after set");
            Check.Equal(IPAddress.Parse("127.0.0.3"), s.Ip, "Ip after set");
            Check.Throws<ArgumentNullException>(() => s.Hostname = null, "null hostname");
            Check.Throws<ArgumentException>(() => s.Hostname = "no-such-host.invalid", "unresolvable hostname");
            Check.Equal("127.0.0.3", s.Hostname, "Hostname unchanged after failed set");
        }

        private static void NfsSettingsPropertyValidation()
        {
            NfsSettings s = ValidNfs();
            Check.Throws<ArgumentOutOfRangeException>(() => s.Port = 0, "Port 0");
            Check.Throws<ArgumentOutOfRangeException>(() => s.Port = 65536, "Port 65536");
            Check.Throws<ArgumentOutOfRangeException>(() => s.MountPort = -1, "MountPort -1");
            Check.Throws<ArgumentOutOfRangeException>(() => s.MountPort = 65536, "MountPort 65536");
            s.MountPort = 0;
            s.MountPort = 20048;
            Check.Equal(20048, s.MountPort, "MountPort");
            Check.Throws<ArgumentOutOfRangeException>(() => s.PortmapperPort = 0, "PortmapperPort 0");
            Check.Throws<ArgumentOutOfRangeException>(() => s.UserId = -1, "UserId -1");
            Check.Throws<ArgumentOutOfRangeException>(() => s.GroupId = -1, "GroupId -1");
            Check.Throws<ArgumentNullException>(() => s.MachineName = null, "null MachineName");
            Check.Throws<ArgumentNullException>(() => s.MachineName = "", "empty MachineName");
            Check.Throws<ArgumentOutOfRangeException>(() => s.ConnectTimeoutMs = 0, "ConnectTimeoutMs 0");
            Check.Throws<ArgumentOutOfRangeException>(() => s.ResponseTimeoutMs = 0, "ResponseTimeoutMs 0");
            Check.Throws<ArgumentNullException>(() => s.Share = null, "null Share");

            s.WriteStability = NfsWriteStabilityEnum.FileSync;
            Check.Equal(NfsWriteStabilityEnum.FileSync, s.WriteStability, "WriteStability");
            s.MachineName = "blobject-host";
            Check.Equal("blobject-host", s.MachineName, "MachineName");
        }

        private static void NfsSettingsShareNormalization()
        {
            NfsSettings s = ValidNfs();
            s.Share = "/export/";
            Check.Equal("/export", s.Share, "trailing slash");
            s.Share = "\\export\\data\\";
            Check.Equal("/export/data", s.Share, "backslashes");
            s.Share = "/";
            Check.Equal("/", s.Share, "root export");
        }

        private static void NfsClientConstructorValidation()
        {
            Check.Throws<ArgumentNullException>(() => new NfsBlobClient(null), "null settings");
            Check.Throws<ArgumentException>(() => new NfsBlobClient(new NfsSettings()), "settings without share");
            Check.Throws<NotSupportedException>(() => new NfsBlobClient(new NfsSettings("127.0.0.1", 0, 0, "/e", NfsVersionEnum.V2)), "NFSv2");
            Check.Throws<NotSupportedException>(() => new NfsBlobClient(new NfsSettings("127.0.0.1", 0, 0, "/e", NfsVersionEnum.V4)), "NFSv4");

            // construction does not connect, so an unreachable server is fine
            using (NfsBlobClient client = new NfsBlobClient(new NfsSettings("127.0.0.1", 0, 0, "/e", NfsVersionEnum.V3) { Port = 1, MountPort = 1 }))
            {
                Check.Equal(4, client.MaxConcurrency, "default MaxConcurrency");
            }
        }

        private static void NfsGenerateUrl()
        {
            using (NfsBlobClient client = new NfsBlobClient(ValidNfs()))
            {
                Check.Equal("nfs://127.0.0.1/export/file.txt", client.GenerateUrl("file.txt"), "root file");
                Check.Equal("nfs://127.0.0.1/export/dir/sub/file.txt", client.GenerateUrl("dir\\sub\\file.txt"), "nested file with backslashes");
                Check.Equal("nfs://127.0.0.1/export/", client.GenerateUrl(""), "empty key");
            }

            NfsSettings custom = new NfsSettings("127.0.0.1", 0, 0, "/", NfsVersionEnum.V3) { Port = 12049 };
            using (NfsBlobClient client = new NfsBlobClient(custom))
            {
                Check.Equal("nfs://127.0.0.1:12049/a.txt", client.GenerateUrl("/a.txt"), "custom port and root export");
            }
        }

        private static async Task NfsArgumentValidationBeforeIo(CancellationToken token)
        {
            using (NfsBlobClient client = new NfsBlobClient(new NfsSettings("127.0.0.1", 0, 0, "/e", NfsVersionEnum.V3) { Port = 1, MountPort = 1, ConnectTimeoutMs = 1000 }))
            {
                await AssertKeyValidation(client, token).ConfigureAwait(false);
            }
        }

        private static void NfsEnumSerialization()
        {
            Check.Equal("\"V3\"", JsonSerializer.Serialize(NfsVersionEnum.V3), "NfsVersionEnum");
            Check.Equal(NfsVersionEnum.V4, JsonSerializer.Deserialize<NfsVersionEnum>("\"V4\""), "NfsVersionEnum round trip");
            Check.Equal("\"DataSync\"", JsonSerializer.Serialize(NfsWriteStabilityEnum.DataSync), "NfsWriteStabilityEnum");
            Check.Equal(NfsWriteStabilityEnum.FileSync, JsonSerializer.Deserialize<NfsWriteStabilityEnum>("\"FileSync\""), "NfsWriteStabilityEnum round trip");
            Check.Keys(new[] { "V2", "V3", "V4" }, Enum.GetNames(typeof(NfsVersionEnum)), "NfsVersionEnum members");
            Check.Keys(new[] { "Unstable", "DataSync", "FileSync" }, Enum.GetNames(typeof(NfsWriteStabilityEnum)), "NfsWriteStabilityEnum members");
        }

        private static void NfsClientInterfaces()
        {
            Check.True(typeof(IDisposable).IsAssignableFrom(typeof(NfsBlobClient)), "IDisposable");
            Check.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(NfsBlobClient)), "IAsyncDisposable");
            Check.True(typeof(BlobClientBase).IsAssignableFrom(typeof(NfsBlobClient)), "BlobClientBase");
        }

        private static void NfsPublicSurface()
        {
            AssertSurface(typeof(NfsBlobClient), new[]
            {
                "C:(NfsSettings)",
                "M:DeleteAsync(String,CancellationToken):Task",
                "M:DeleteManyAsync(IEnumerable`1,CancellationToken):Task`1",
                "M:Dispose():Void",
                "M:DisposeAsync():ValueTask",
                "M:EmptyAsync(CancellationToken):Task`1",
                "M:Enumerate(EnumerationFilter):IEnumerable`1",
                "M:EnumerateAsync(EnumerationFilter,CancellationToken):IAsyncEnumerable`1",
                "M:ExistsAsync(String,CancellationToken):Task`1",
                "M:GenerateUrl(String,CancellationToken):String",
                "M:GetAsync(String,CancellationToken):Task`1",
                "M:GetMetadataAsync(String,CancellationToken):Task`1",
                "M:GetStreamAsync(String,CancellationToken):Task`1",
                "M:ListShares(CancellationToken):Task`1",
                "M:ValidateConnectivity(CancellationToken):Task`1",
                "M:WriteAsync(String,String,Byte[],CancellationToken):Task",
                "M:WriteAsync(String,String,Int64,Stream,CancellationToken):Task",
                "M:WriteAsync(String,String,String,CancellationToken):Task",
                "M:WriteManyAsync(List`1,CancellationToken):Task"
            });

            AssertSurface(typeof(NfsSettings), new[]
            {
                "C:()",
                "C:(String,Int32,Int32,String,NfsVersionEnum)",
                "P:ConnectTimeoutMs:Int32:get,set",
                "P:GroupId:Int32:get,set",
                "P:Hostname:String:get,set",
                "P:Ip:IPAddress:get",
                "P:MachineName:String:get,set",
                "P:MountPort:Int32:get,set",
                "P:Port:Int32:get,set",
                "P:PortmapperPort:Int32:get,set",
                "P:ResponseTimeoutMs:Int32:get,set",
                "P:Share:String:get,set",
                "P:UserId:Int32:get,set",
                "P:Version:NfsVersionEnum:get,set",
                "P:WriteStability:NfsWriteStabilityEnum:get,set"
            });
        }

        private static void NfsDependencies()
        {
            AssemblyName[] references = typeof(NfsBlobClient).Assembly.GetReferencedAssemblies();
            Check.True(references.Any(r => r.Name == "OpenNFS.Client"), "references OpenNFS.Client");
            Check.False(references.Any(r => r.Name.StartsWith("NFSLibrary", StringComparison.OrdinalIgnoreCase) || r.Name.StartsWith("NFS-Client", StringComparison.OrdinalIgnoreCase)), "does not reference NFS-Client");
        }

        #endregion

        #region Shared

        private static async Task AssertKeyValidation(BlobClientBase client, CancellationToken token)
        {
            await Check.ThrowsAsync<ArgumentNullException>(() => client.GetAsync(null, token), "GetAsync null").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.GetStreamAsync(null, token), "GetStreamAsync null").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.GetMetadataAsync(null, token), "GetMetadataAsync null").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.WriteAsync(null, "text/plain", new byte[1], token), "WriteAsync null").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.DeleteAsync(null, token), "DeleteAsync null").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.ExistsAsync(null, token), "ExistsAsync null").ConfigureAwait(false);

            foreach (string key in new[] { "", "/", "\\\\", "..", "../x", "a/../b", "./x", "a/./b" })
            {
                await Check.ThrowsAsync<ArgumentException>(() => client.GetAsync(key, token), "GetAsync '" + key + "'").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentException>(() => client.WriteAsync(key, "text/plain", "x", token), "WriteAsync '" + key + "'").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentException>(() => client.ExistsAsync(key, token), "ExistsAsync '" + key + "'").ConfigureAwait(false);
            }

            await Check.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteAsync("x.bin", "application/octet-stream", -1, new System.IO.MemoryStream(), token), "negative content length").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.WriteAsync("x.bin", "application/octet-stream", 1, null, token), "null stream with length").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.WriteManyAsync(null, token), "WriteManyAsync null").ConfigureAwait(false);
            await Check.ThrowsAsync<ArgumentNullException>(() => client.DeleteManyAsync(null, token), "DeleteManyAsync null").ConfigureAwait(false);

            DeleteManyResult none = await client.DeleteManyAsync(new string[0], token).ConfigureAwait(false);
            Check.Equal(0, none.Results.Count, "DeleteManyAsync of no keys does no I/O");
            await client.WriteManyAsync(new List<WriteRequest>(), token).ConfigureAwait(false);
        }

        private static void AssertSurface(Type type, IEnumerable<string> expected)
        {
            List<string> actual = new List<string>();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (ConstructorInfo c in type.GetConstructors(flags))
            {
                actual.Add("C:(" + String.Join(",", c.GetParameters().Select(p => p.ParameterType.Name)) + ")");
            }

            foreach (MethodInfo m in type.GetMethods(flags).Where(m => !m.IsSpecialName))
            {
                actual.Add("M:" + m.Name + "(" + String.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) + "):" + m.ReturnType.Name);
            }

            foreach (PropertyInfo p in type.GetProperties(flags))
            {
                List<string> accessors = new List<string>();
                if (p.GetGetMethod() != null) accessors.Add("get");
                if (p.GetSetMethod() != null) accessors.Add("set");
                actual.Add("P:" + p.Name + ":" + p.PropertyType.Name + ":" + String.Join(",", accessors));
            }

            Check.Keys(expected, actual, type.Name + " public surface");
        }

        #endregion
    }
}
