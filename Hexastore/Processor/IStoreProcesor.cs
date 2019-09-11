﻿using System.Collections.Generic;
using Hexastore.Graph;
using Newtonsoft.Json.Linq;

namespace Hexastore.Processor
{
    public interface IStoreProcesor
    {
        void Assert(string storeId, JToken value, bool strict);
        void Patch(string storeId, JObject input);
        void AssertMeta(string storeId, JObject value);
        JObject GetSet(string storeId);
        (IStoreGraph, IStoreGraph, IStoreGraph) GetGraphs(string storeId);
        JObject GetSubject(string storeId, string subject, string[] expand, int level);
        JObject GetType(string storeId, string[] type, string[] expand, int level);
        JObject Query(string storeId, JObject query, string[] expand, int level);
    }
}