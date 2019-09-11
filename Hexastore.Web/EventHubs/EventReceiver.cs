﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Hexastore.Processor;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hexastore.Web.EventHubs
{

    public class EventReceiver : IPartitionReceiveHandler
    {
        private readonly Dictionary<string, TaskCompletionSource<bool>> _completions = new Dictionary<string, TaskCompletionSource<bool>>();
        private readonly ILogger<EventReceiver> _logger;
        private readonly Checkpoint _checkpoint;
        private readonly IStoreProcesor _storeProcessor;

        private int _eventCount;

        public EventReceiver(IStoreProcesor storeProcessor, Checkpoint checkpoint, ILogger<EventReceiver> logger)
        {
            _logger = logger;
            _checkpoint = checkpoint;
            _storeProcessor = storeProcessor;

            _eventCount = 0;
            _ = LogCount();
        }

        public int MaxBatchSize
        {
            get
            {
                return 1000;
            }
            set
            {
            }
        }

        private async Task LogCount()
        {
            var lastCount = 0;
            while (true) {
                await Task.Delay(10000);
                _logger.LogInformation($"{DateTime.Now.ToString("hh':'mm':'ss")} Events: {_eventCount} Diff: {_eventCount - lastCount}");
                lastCount = _eventCount;
            }
        }

        public async Task ProcessEventsAsync(IEnumerable<EventData> events)
        {
            if (events == null) {
                return;
            }

            foreach (var e in events) {
                _eventCount++;
                var content = Encoding.UTF8.GetString(e.Body);
                var o = JObject.Parse(content);
                await ProcessEventsAsync(o);
                _checkpoint.Write(Constants.EventHubCheckpoint, e.SystemProperties["x-opt-offset"].ToString());
            }

            return;
        }

        public Task ProcessEventsAsync(JObject o)
        {
            var storeId = o["storeId"]?.Value<string>();

            var operation = o["operation"]?.Value<string>();
            var opId = o["operationId"]?.Value<string>();
            var strict = o["strict"]?.Value<bool>();
            var data = o["data"];
            TaskCompletionSource<bool> tc = null;
            if (opId != null) {
                _completions.TryGetValue(opId, out tc);
            }

            try {
                switch (operation) {
                    case "POST":
                        _storeProcessor.Assert(storeId, data, strict ?? false);
                        break;
                    case "PATCH":
                        _storeProcessor.Patch(storeId, (JObject)data);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown operation {operation}");
                }
                tc?.SetResult(true);
            } catch (Exception exception) {
                tc?.SetException(exception);
            } finally {
                if (tc != null) {
                    _completions.Remove(opId);
                }
            }
            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(Exception error)
        {
            Console.WriteLine(JsonConvert.SerializeObject(error));
            return Task.CompletedTask;
        }

        public void SetCompletion(string guid, TaskCompletionSource<bool> tc)
        {
            _completions[guid] = tc;
        }
    }
}