using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.StatisticModels
{
    public class StatisticsModel
    {
        public long CustomerCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public long TotalArtworkCount { get; set; }
        public Dictionary<string, long> ArtworkCountByStatus { get; set; }
        public long TransactionCount { get; set; }
        public long JobCount { get; set; }
        public long PortfolioCount { get; set; }
        public long FreelancerServiceCount { get; set; }
        public Dictionary<string, long> ProPackagePurchaseCount { get; set; } // Số lượng giao dịch mỗi gói
        public Dictionary<string, decimal> ProPackageRevenue { get; set; }
    }
}
