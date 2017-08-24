using Sitecore.Abstractions;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Pipelines.GetContextIndex;
using Sitecore.Diagnostics;
using Sitecore.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sitecore.Support.ContentSearch.Pipelines.GetContextIndex
{
    public class FetchIndex : Sitecore.ContentSearch.Pipelines.GetContextIndex.FetchIndex
    {
        private ISettings settings;
        public FetchIndex()
        {
            //Sitecore.Support.171629
            FieldInfo fieldInfo = typeof(Sitecore.ContentSearch.Pipelines.GetContextIndex.FetchIndex).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
            this.settings = (ISettings)fieldInfo.GetValue(this);
            //
        }
        protected override string GetContextIndex(IIndexable indexable, GetContextIndexArgs args)
        {
            if (indexable == null)
            {
                return null;
            }
            List<ISearchIndex> source = new List<ISearchIndex>();
            AbstractSearchIndex tempIndex;

            foreach (var searchIndex in ContentSearchManager.Indexes)
            {
                tempIndex = searchIndex as AbstractSearchIndex;
                //Sitecore.Support.171629
                if ((tempIndex == null || tempIndex.IsInitialized) &&
                    searchIndex.Crawlers.Any(c => !c.IsExcludedFromIndex(indexable) && c.GetType().GetInterfaces().Contains(typeof(IContextIndexRankable))))
                //
                {
                    source.Add(searchIndex);
                }
            }

            IEnumerable<ISearchIndex> enumerable = source.AsEnumerable<ISearchIndex>();
            if (!enumerable.Any<ISearchIndex>())
            {
                enumerable = this.FindIndexesRelatedToIndexable(args.Indexable, ContentSearchManager.Indexes);
            }
            IEnumerable<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>> enumerable2 = this.RankContextIndexes(enumerable, indexable);
            Sitecore.Caching.Generics.Tuple<ISearchIndex, int>[] tupleArray = (enumerable2 as Sitecore.Caching.Generics.Tuple<ISearchIndex, int>[]) ?? enumerable2.ToArray<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>();
            if (!tupleArray.Any<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>())
            {
                Log.Error(string.Format("There is no appropriate index for {0} - {1}. You have to add an index crawler that will cover this item", indexable.AbsolutePath, indexable.Id), this);
                return null;
            }
            if (tupleArray.Count<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>() == 1)
            {
                return tupleArray.First<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>().First.Name;
            }
            if (tupleArray.First<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>().Second < tupleArray.Skip<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>(1).First<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>().Second)
            {
                return tupleArray.First<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>().First.Name;
            }
            string setting = settings.GetSetting("ContentSearch.DefaultIndexType", "");
            Type defaultType = ReflectionUtil.GetTypeInfo(setting);
            if (defaultType == null)
            {
                return tupleArray[0].First.Name;
            }
            //Sitecore.Support.171629
            var higherRank = tupleArray[0].Second;
            Sitecore.Caching.Generics.Tuple<ISearchIndex, int>[] tupleArray2 = (from i in tupleArray
                                                                                where (i.First.GetType() == defaultType || i.First.GetType().IsSubclassOf(defaultType)) && i.Second == higherRank
                                                                                orderby i.First.Name
                                                                                select i).ToArray<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>();
            //
            if (!tupleArray2.Any<Sitecore.Caching.Generics.Tuple<ISearchIndex, int>>())
            {
                return tupleArray[0].First.Name;
            }
            return tupleArray2[0].First.Name;
        }
    }
}