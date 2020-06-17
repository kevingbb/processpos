using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.ServiceBus;

namespace processposapi
{
    public partial class SalesEventsTable : TableEntity
    {
        public string Text { get; set; }
    }
    public static class processpos
    {
        [FunctionName("processpos")]
        public static async Task Run([EventHubTrigger("khsvrlessohehpos", Connection = "POSStorage")] EventData[] events,
            [Table("salesevents"), StorageAccount("AzureWebJobsStorage")] CloudTable msg,
            [ServiceBus("khsvrlessohsbtopic", Connection = "PUBSUBConnection")] IAsyncCollector<Message> topicCollector,
            ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                log.LogInformation($"Event: {Encoding.UTF8.GetString(eventData.Body)}");
                // Metadata accessed by binding to EventData
                log.LogInformation($"EnqueuedTimeUtc={eventData.SystemProperties.EnqueuedTimeUtc}");
                log.LogInformation($"SequenceNumber={eventData.SystemProperties.SequenceNumber}");
                log.LogInformation($"Offset={eventData.SystemProperties.Offset}");
                string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                dynamic data = JObject.Parse(messageBody);
                string salesNumber = data.header.salesNumber;

                try
                {
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                    SalesEventsTable salesEventsTable = new SalesEventsTable();
                    salesEventsTable.PartitionKey = "SalesEvents";
                    salesEventsTable.RowKey = salesNumber;
                    salesEventsTable.Text = messageBody;
                    var operation = TableOperation.Insert(salesEventsTable);
                    await msg.ExecuteAsync(operation);
                    log.LogInformation($"Added salesevents entry {salesNumber} to SalesEventsTable completed.");

                    // Add to Pub/Sub only if Receipt Exists
                    log.LogInformation($"Started Pub/Sub entry {salesNumber}.");
                    if ((string)data.header.receiptUrl != null)
                    {
                        log.LogInformation($"Pub/Sub entry {salesNumber} receipt found.");
                        PubSubMessage pubSubContent = new PubSubMessage();
                        pubSubContent.TotalItems = data.details.Count;
                        pubSubContent.TotalCost = data.header.totalCost;
                        pubSubContent.SalesNumber = data.header.salesNumber;
                        pubSubContent.SalesDate = data.header.dateTime;
                        pubSubContent.StoreLocation = data.header.locationId;
                        pubSubContent.ReceiptUrl = data.header.receiptUrl;
                        string psContent = JsonConvert.SerializeObject(pubSubContent);
                        log.LogInformation($"psContent: {psContent}");
                        Microsoft.Azure.ServiceBus.Message message = new Microsoft.Azure.ServiceBus.Message();
                        message.Body = Encoding.UTF8.GetBytes(psContent);
                        message.UserProperties.Add("totalCost", (double)data.header.totalCost);
                        await topicCollector.AddAsync(message);
                        //await Task.FromResult<Message>(message);
                    }
                    else
                    {
                        log.LogInformation($"Pub/Sub entry {salesNumber} no receipt found.");
                    }
                    log.LogInformation($"Started Pub/Sub entry {salesNumber} completed.");
                }
                catch (Microsoft.Azure.Cosmos.Table.StorageException exc)
                {
                    if (exc.RequestInformation.HttpStatusCode == 409)
                    {
                        log.LogInformation($"HttpStatusCode 409, entity already exists, handled for {salesNumber}.");
                    }
                    else
                    {
                        log.LogError($"Storing entry {salesNumber} to salesevents Table failed: {exc.Message}");
                        exceptions.Add(exc);
                    }
                }
                catch (Exception exc)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    log.LogError($"Storing entry {salesNumber} to salesevents Table failed with exception type {exc.GetType().ToString()} and error message: {exc.Message}");
                    exceptions.Add(exc);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.
            if (exceptions.Count > 1)
            {
                log.LogError($"Event Hub Trigger function processpos threw {exceptions.Count} exceptions.");
                throw new AggregateException(exceptions);
            }

            if (exceptions.Count == 1)
            {
                log.LogError($"Event Hub Trigger function processpos threw {exceptions.Count} exceptions.");
                throw exceptions.Single();
            }
        }
    }
}
