using Repositories.Models.ProPackageModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Models.AccountProPackageModels
{
    public class AccountProPackageInfos
    {
        public Guid Id { get; set; }
        public Guid ProPackageId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PackageStatus { get; set; }
        public ProPackageDTO ProPackage { get; set; }
    }
}
