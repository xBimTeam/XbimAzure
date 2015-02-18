using System;
using System.ComponentModel;
using System.IO;

namespace XbimCloudCommon
{
    public class XbimCloudModel
    {
        public XbimCloudModel()
        {
            //initial value created from Guid which is always unique
            ModelId = Guid.NewGuid().ToString();
        }
        public string ModelId { get;  set; }

        public string Extension { get; set; }

    }
}
