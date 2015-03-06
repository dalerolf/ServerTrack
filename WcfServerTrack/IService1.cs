using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace WcfServerTrack
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]
    public interface IService1
    {
        [OperationContract]
        void RecordStats(string serverName, LoadStat loadStats);

        [OperationContract]
        LoadReport FetchServerStats(string serverName);
    }

    [DataContract]
    public class LoadReport
    {
        List<IndexedLoadStat> averageHourLoadByMinute;
        List<IndexedLoadStat> averageDailyLoadByHour;

        [DataMember]
        public List<IndexedLoadStat> AverageHourLoadByMinute
        {
            get { return averageHourLoadByMinute; }
            set { averageDailyLoadByHour = value;}
        }

        [DataMember]
        public List<IndexedLoadStat> AverageDailyLoadByHour
        {
            get { return averageDailyLoadByHour; }
            set { averageDailyLoadByHour = value;}
        }
    }


    public class IndexedLoadStat
    {
        LoadStat loadStat;
        int index;

        public LoadStat LoadStat
        {
            get { return loadStat; }
            set { loadStat = value; }
        }        

        public int Index
        {
            get { return index; }
            set { index = value; }
        }
    }

    // Use a data contract as illustrated in the sample below to add composite types to service operations.
    [DataContract]
    public class LoadStat
    {
        double cpuLoad;
        double memoryLoad;

        [DataMember]
        public double CpuLoad
        {
            get { return cpuLoad; }
            set { cpuLoad = value; }
        }

        [DataMember]
        public double MemoryLoad
        {
            get { return memoryLoad; }
            set { memoryLoad = value; }
        }
    }
}
