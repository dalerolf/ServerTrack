using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Collections.Concurrent;

namespace WcfServerTrack
{
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IService1
    {
        ConcurrentDictionary<string, ConcurrentDictionary<long, LoadStat>> serversStats = new ConcurrentDictionary<string, ConcurrentDictionary<long, LoadStat>>();

        // Excersize calls for serverName(string), cpuLoad(double) and ramLoad(double) which really limits this as the
        // server is forced to provide the time stamp which given a HTTP system can be delayed substantially from the 
        // send time.  It also leaves the server up to the job of sorting out record colision which is more difficult
        // given the user of server name not MAC address as there can be N machines claiming the same name but it is
        // much harder to have a working network with N machines using the same MAC address.

        // Lots of questions on excersize as to what the use intent of this is.  Done for VMs this is pretty low value
        // as many VM hosting systems support dynamic CPU/Memroy systems now which makes a % value a low value data 
        // point.  
        void IService1.RecordStats(string serverName, LoadStat loadStats)
        {
            // Force casing to reduce the string matching costs.  
            string localServerName = serverName.ToUpperInvariant();

            // Load thoughts - if load is a problem using the int HashCode value of the server name would allow the 
            // data set to be better fragemented with a small memory hit to hold the N array at startup.

            // Nit - this should use a lamba or functor to avoid memory churn on pre-allocation but its been to long...
            var serverStats = serversStats.GetOrAdd(localServerName, new ConcurrentDictionary<long, LoadStat>());
            
            // This can fail if somehow the UTC filetime for the server already exists.  Since the excersize does not
            // cover the issue of data overlap we'll go with first in wins.
            serverStats.TryAdd(DateTime.Now.ToFileTimeUtc(), loadStats);
        }

        LoadReport IService1.FetchServerStats(string serverName)
        {
            LoadReport report = new LoadReport();

            // Force casing to reduce the string matching costs.  
            string localServerName = serverName.ToUpperInvariant();
            ConcurrentDictionary<long, LoadStat> serverStats;

            if(serversStats.TryGetValue(localServerName, out serverStats))
            {
                // Guese on better default capacity than standard value - if i recall right the growth algorithm here is 
                // basic doubleing but anything is better than the default of 0
                report.AverageHourLoadByMinute = new List<IndexedLoadStat>(15);
                report.AverageDailyLoadByHour = new List<IndexedLoadStat>(8);

                DateTime timeHack = DateTime.Now;
                var startTime = timeHack.ToFileTimeUtc();
                var endTime = timeHack.AddDays(-1).ToFileTimeUtc();                

                // Clone a list of the keys so that we can figure out which values to use without interferring with
                // collection concurrency
                var UtcTimes = serverStats.Keys.ToArray();

                var dayData = from t in UtcTimes
                              where t <= startTime && t >= endTime
                              orderby t
                              select t;

                // We can accumulate the totals into the report structure but we need to keep track of how many
                // entries where accumulated
                Dictionary<int, int> minuteEntries = new Dictionary<int, int>(60);
                Dictionary<int, int> hourEntries = new Dictionary<int, int>(24);
                
                foreach(var utcFileTime in dayData)
                {
                    DateTime utcTime = DateTime.FromFileTimeUtc(utcFileTime);
                    var timeSpan = timeHack - utcTime;
                    LoadStat loadStat;

                    // timespan of 0 hours means this is entry needs to be processed for the "hours" and "minutes" 
                    // buckets
                    if(timeSpan.Hours == 0)
                    {
                        if(serverStats.TryGetValue(utcFileTime, out loadStat))
                        {
                            bool found = false;
                            foreach(var i in report.AverageHourLoadByMinute)
                            {
                                if(i.Index == timeSpan.Minutes)
                                {
                                    i.LoadStat.CpuLoad += loadStat.CpuLoad;
                                    i.LoadStat.MemoryLoad += loadStat.MemoryLoad;
                                    found = true;
                                }
                                if(found == false)
                                {
                                    IndexedLoadStat indexLoadStat = new IndexedLoadStat();
                                    indexLoadStat.Index = timeSpan.Minutes;
                                    indexLoadStat.LoadStat = loadStat;
                                    report.AverageHourLoadByMinute.Add(indexLoadStat);
                                }
                            }
                            minuteEntries[timeSpan.Minutes]++;
                        }
                    }
                    
                    if (serverStats.TryGetValue(utcFileTime, out loadStat))
                    {
                        bool found = false;
                        foreach (var i in report.AverageDailyLoadByHour)
                        {
                            if (i.Index == timeSpan.Hours)
                            {
                                i.LoadStat.CpuLoad += loadStat.CpuLoad;
                                i.LoadStat.MemoryLoad += loadStat.MemoryLoad;
                                found = true;
                            }
                            if (found == false)
                            {
                                IndexedLoadStat indexLoadStat = new IndexedLoadStat();
                                indexLoadStat.Index = timeSpan.Hours;
                                indexLoadStat.LoadStat = loadStat;
                                report.AverageDailyLoadByHour.Add(indexLoadStat);
                            }
                        }
                        hourEntries[timeSpan.Hours]++;
                    }
                }
                
                // Now we have accumulations for minutes and hours average them out
                foreach(var indexedLoadStat in report.AverageDailyLoadByHour)
                {
                    indexedLoadStat.LoadStat.CpuLoad /= hourEntries[indexedLoadStat.Index];
                    indexedLoadStat.LoadStat.MemoryLoad /= hourEntries[indexedLoadStat.Index];
                }
                foreach(var indexedLoadStat in report.AverageHourLoadByMinute)
                {
                    indexedLoadStat.LoadStat.CpuLoad /= minuteEntries[indexedLoadStat.Index];
                    indexedLoadStat.LoadStat.MemoryLoad /= minuteEntries[indexedLoadStat.Index];
                }
            }
            else
            {
                report.AverageHourLoadByMinute = new List<IndexedLoadStat>(0);
                report.AverageDailyLoadByHour = new List<IndexedLoadStat>(0);
            }

            return report;            
        }
    }
}
