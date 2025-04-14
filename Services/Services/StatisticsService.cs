using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Repositories.Entities;
using Repositories.Enums;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Models.StatisticModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<Account> _userManager;
        private readonly RoleManager<Repositories.Entities.Role> _roleManager;
        private readonly ILogger<StatisticsService> _logger;

        public StatisticsService(
            IUnitOfWork unitOfWork,
            UserManager<Account> userManager,
            RoleManager<Repositories.Entities.Role> roleManager,
            ILogger<StatisticsService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StatisticsModel> GetStatisticsAsync()
        {
            _logger.LogInformation("Bắt đầu lấy thống kê...");

            // 1. Đếm số lượng khách hàng (role = "Customer")
            var customerRole = await _roleManager.FindByNameAsync(Repositories.Enums.Role.Customer.ToString());
            long customerCount = 0;
            if (customerRole != null)
            {
                var customers = await _userManager.GetUsersInRoleAsync(Repositories.Enums.Role.Customer.ToString());
                customerCount = customers.Count(u => !u.IsDeleted);
            }
            else
            {
                _logger.LogWarning("Không tìm thấy vai trò Customer.");
            }

            // 2. Tính tổng doanh thu từ giao dịch hoàn tất
            var totalRevenue = await _unitOfWork.TransactionRepository
                .SumAsync(
                    t => !t.IsDeleted && t.PaymentStatus == PaymentStatus.Complete,
                    t => t.MoneyAmount
                );

            // 3. Đếm tổng số artwork
            var totalArtworkCount = await _unitOfWork.ArtworkRepository
                .CountAsync(a => !a.IsDeleted);

            // 4. Đếm số lượng artwork theo trạng thái
            var artworkByStatus = await _unitOfWork.ArtworkRepository
                .GroupByAsync(
                    filter: a => !a.IsDeleted,
                    groupBy: a => a.Status,
                    select: g => new { Status = g.Key, Count = g.Count() }
                );
            var artworkCountByStatus = artworkByStatus
                .ToDictionary(
                    x => x.Status.ToString(),
                    x => (long)x.Count
                );

            // 5. Đếm số lượng giao dịch
            var transactionCount = await _unitOfWork.TransactionRepository
                .CountAsync(t => !t.IsDeleted);

            // 6. Đếm số lượng công việc (jobs)
            var jobCount = await _unitOfWork.JobRepository
                .CountAsync(j => !j.IsDeleted);

            // 7. Đếm số lượng danh mục đầu tư (portfolios)
            var portfolioCount = await _unitOfWork.PortfolioRepository
                .CountAsync(p => !p.IsDeleted);

            // 8. Đếm số lượng dịch vụ freelancer
            var freelancerServiceCount = await _unitOfWork.FreelancerServiceRepository
                .CountAsync(fs => !fs.IsDeleted);

            // 9. Thống kê số lượng giao dịch và tổng tiền của từng ProPackage
            var proPackageStats = await _unitOfWork.TransactionRepository
                .GroupByAsync(
                    filter: t => !t.IsDeleted && t.PaymentStatus == PaymentStatus.Complete && t.ProPackageId != null,
                    groupBy: t => t.ProPackageId,
                    select: g => new
                    {
                        ProPackageId = g.Key,
                        PurchaseCount = g.Count(),
                        TotalRevenue = g.Sum(t => t.MoneyAmount)
                    }
                );

            // Lấy danh sách tất cả ProPackage để ánh xạ tên
            var proPackages = await _unitOfWork.ProPackageRepository
                .GetAllAsync(
                    filter: p => !p.IsDeleted,
                    pageIndex: 1,
                    pageSize: int.MaxValue
                );

            var proPackagePurchaseCount = new Dictionary<string, long>();
            var proPackageRevenue = new Dictionary<string, decimal>();

            foreach (var stat in proPackageStats)
            {
                var package = proPackages.Data.FirstOrDefault(p => p.Id == stat.ProPackageId);
                var packageKey = package != null ? package.Name ?? stat.ProPackageId.ToString() : stat.ProPackageId.ToString();
                proPackagePurchaseCount[packageKey] = stat.PurchaseCount;
                proPackageRevenue[packageKey] = stat.TotalRevenue;
            }

            _logger.LogInformation("Lấy thống kê thành công.");

            return new StatisticsModel
            {
                CustomerCount = customerCount,
                TotalRevenue = totalRevenue,
                TotalArtworkCount = totalArtworkCount,
                ArtworkCountByStatus = artworkCountByStatus,
                TransactionCount = transactionCount,
                JobCount = jobCount,
                PortfolioCount = portfolioCount,
                FreelancerServiceCount = freelancerServiceCount,
                ProPackagePurchaseCount = proPackagePurchaseCount,
                ProPackageRevenue = proPackageRevenue
            };
        }
    }
}