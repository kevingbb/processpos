using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace processposapi
{
    public partial class PubSubMessage
    {
        [JsonProperty("totalItems")]
        public long TotalItems { get; set; }

        [JsonProperty("totalCost")]
        public double TotalCost { get; set; }

        [JsonProperty("salesNumber")]
        public string SalesNumber { get; set; }

        [JsonProperty("salesDate")]
        public string SalesDate { get; set; }

        [JsonProperty("storeLocation")]
        public string StoreLocation { get; set; }

        [JsonProperty("receiptUrl")]
        public string ReceiptUrl { get; set; }
    }
}