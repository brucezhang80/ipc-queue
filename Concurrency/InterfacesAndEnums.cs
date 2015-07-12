using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
//using Apache.NMS;
//using com.alphasystematics.etrader.ExcelModel.nms;
//using Spring.Messaging.Nms.Support.Converter;

namespace com.alphaSystematics.concurrency
{
    public enum DataStructureType
    {
        Default,
        Queue,
        Stack
    }






    /************************
    public interface MMFile
    {
        string IPCFileName { get; set; }
        int FileSize { get; set; }
        int ViewSize { get; set; }
        int Capacity { get; set; }
        bool IsFull();
        int Count { get; set; }
        int Length { get; }
        void Close();
        void Enqueue<T>(int viewIndex, T writeData) where T : struct;
        T Dequeue<T>(int viewIndex) where T : struct;
        void Enqueue<T>(int viewIndex, T[] writeData) where T : struct;
        int Dequeue<T>(int viewIndex, out T[] readData) where T : struct;
    }
    ************************/

    /************************
    public interface MMFile<T>
    {
        string IPCFileName { get ; set; } 
        int FileSize { get ; set ; } 
        int ViewSize { get ; set ; } 
        int Capacity { get ; set ; } 
        bool IsFull();
        int Count { get ; set; } 
        int Length { get ; } 
        void Enqueue(int viewIndex, T data);
        T Dequeue<T>(int viewIndex);
        void Close();
    }
    *****************************************/
    //public interface IStockService
    //{
    //    void Send(TradeRequest tradeRequest);
    //}

    //public interface INamedMessageConverter : IMessageConverter
    //{
    //     string Name { get; set; }
    //     Type TargetType { get; }
    //}

    ///// <summary>
    ///// Provides a layer of indirection when adding the 'type' of the object as a message property.
    ///// </summary>
    ///// <author>Mark Pollack</author>
    //public interface ITypeMapper
    //{
    //    string TypeIdFieldName
    //    {
    //        get;
    //    }

    //    string FromType(Type typeOfObjectToConvert);

    //    Type ToType(string typeId);
    //}


    //public interface IBasicMessageHandler
    //{
    //    void ReceiveMessage(string message);

    //    void ReceiveMessage(Hashtable message);

    //    void ReceiveMessage(byte[] message);
    //}

    //public interface ITextMessageHandler 
    //{
    //    void ReceiveMessage(ITextMessage message);
    //}


    /*******************************************************

    public interface IViewValueData
    {
        string GetDisplayName();
        string GetValueName();
        IList GetListWithAll();
        IList GetListWithoutAll();
        object GetDefaultValue();
        void SetCurrentValue(object obj);
        object GetCurrentValue();
        void SetView(DataView vw);
        void Reset();
    }


    public interface ITradesViewValueFilterData : IViewValueData
    {
        string GetFilterString();
    }

    public interface ITradesFilter
    {
        string GetFilterString();
        TradesFilter GetFilterType();
        object GetFilterValue();
        void SetFilterValue();
    }

    public interface INameIdProvider
    {
        string GetName(string id);
        string GetId(string name);
        string[] Names
        {
            get;
        }
    }

    public interface ISetConsumer2 : ISetConsumer
    {
        event EventHandler SchemaAvailable;
    }


    [Flags]
    public enum TradesFilter
    {
        None = 0,
        SectorTeam = 1,
        Country = 2,
        Date = 4,
        Client = 8,
        Portfolio = 16,
        TradingGroup = 32,
        Valid = 64,
        Trader = 128,
        SalesTrader = 256,
        SalesTeam = 512,
        SalesTraderOrTeam = 1024,
        IOI = 2048,
        DateRange = 4096,
        Status = 8192,
        Cancelled,
        Manual,
        TradeAd,
        Block,
        NoFB,
        Regions,
        TraderOrTeam
    }

    public enum TradeViewColumn
    {
        VwapErrorCode,
        TradeTime,
        ClientName,
        Ric,
        Factor,
        IOI,
        Natural,
        Crossed,
        OASISRef,
        ReportingRef,
        TraderName,
        SalesTraderName,
        ApproverByName,
        ValidatedByName,
        SectorTeamName,
        Side,
        Quantity,
        Price,
        VWAP,
        VwapDifference,
        VWAPVolume,
        MergeForVWAP,
        Notional,
        TheoUSD,
        ActualUSD,
        PNLUSD,
        HedgeUSD,
        TotalPNLUSD,
        TraderNotes,
        Valid,
        Approved,
        UseActual,
        Status,
        Block,
        Manual,
        SalesTraderNotes,
        RiskPremium,
        RiskPremiumRate,
        Net,
        TheoVWAP,
        MergeRef,
        CrossRef,
        Cancelled,
        TradeAd,
        NoFB,
        StockName,
        BloomCode,
        ClientFactor,
        BookFactor,
        FxRate,
        TradeId,
        LOB,
        Franchise,
        RiskPremiumBps
    }

    [Flags]
    public enum TraderRole
    {
        Unknown = 0,
        Trader = 1,
        SalesTrader = 2,
        Administrator = 4,
        Program = 8,
        SuperViewer = 16,
        ProgramTrader = 32,
        ProgramSalesTrader = 64
    }

    public enum TradesFilterType
    {
        All,
        SalesTrader,
        SalesTraderTeam
    }

    public enum SalesTeamType
    {
        Unknown,
        Deriv,
        EuroInst,
        Hedge,
        RiskArb,
        UKInst,
        USInst
    }

    public enum ClientColumns
    {
        Name,
        CRef,
        Factor,
        CMISClient,
        Equator,
        RSP,
        FacBand,
        CMISExtract
    }

    public enum AssetOption
    {
        None,
        Ric,
        StockName,
        Bloomberg
    }

    public enum ReportAgType
    {
        SectorTeam,
        Country,
        Client,
        Date
    }

    // T.Tapper MOD 0002.3 Allow editing of ViewTrades Grid for ActualVWAP & ActualPnLUsd columns - BEGIN
    // The operations that can be performed in grid editing.
    public enum menuItemGridEditActions
    {
        on = 0,
        off = 1,
        save = 2,
        resetall = 3,
        resetcurrent = 4,
        initialise = 5,
        checkPermission = 6
    }

    // Not used yet but may be useful for translation into the trade statuses used in the java server code
    public enum TradeLifeCycle
    {
    	new_trade = 0,
		reference_data_validated = 1,
		part_analysed = 2,
		fully_analysed = 3,
		trader_approved = 4,
		salestrader_approved = 5,
		cmis_reported = 6
    }

    // Used for determining if any of the trades in the grid are editable
    public sealed class TradeStatus
    {
        public static string new_trade = "NW";
        public static string reference_data_validated = "EN";
        public static string part_analysed = "PA";
        public static string fully_analysed = "FA";
        public static string trader_approved = "TA";
        public static string salestrader_approved = "SA";
        public static string cmis_reported = "CM";
    }

    // T.Tapper MOD 0002.3 Allow editing of ViewTrades Grid for ActualVWAP & ActualPnLUsd columns - END

    // T.Tapper MOD 0002.7 SalesTrader approve trades on Trader validation - BEGIN
    // T.Tapper MOD 0002.4 Allow trades with no client - BEGIN
    // Used for determining if any of the trades in the grid are for a region where sales trader approval is 
    // automatically performed when the trade is validated. Currently only Japan.
    public sealed class RegionsList
    {
        public static string Australia = "AU";
        public static string Asia = "AS";
        public static string Japan = "JP";
        public static string All = "XX";
        public static string Other = "OT";

        private static List<String> AutoApproveList = new List<string>();
        private static List<String> AllowEditTradeWithNoClientList = new List<string>();

        static RegionsList()
        {
            AutoApproveList.Add(RegionsList.Japan);
            AllowEditTradeWithNoClientList.Add(RegionsList.Japan);
        }

        public static bool AutoApprove(String region)
        {
            if (AutoApproveList.Contains(region))
            {
                return true;
            }
            else return false;
        }

        public static bool AllowEditTradeWithNoClient(String region)
        {
            if (AllowEditTradeWithNoClientList.Contains(region))
            {
                return true;
            }
            else return false;
        }

    }
    // T.Tapper MOD 0002.4 Allow trades with no client - END
    // T.Tapper MOD 0002.7 SalesTrader approve trades on Trader validation - END
    ********************************************************************/
}