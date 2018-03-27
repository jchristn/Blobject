using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using Amazon.S3;
using Amazon.S3.Model;

namespace BlobHelper
{ 
    public enum AwsRegion
    {
        APNortheast1,
        APSoutheast1,
        APSoutheast2,
        EUWest1,
        SAEast1,
        USEast1,
        USGovCloudWest1,
        USWest1,
        USWest2
    }
}
