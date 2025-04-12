using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore;
using Repositories.Entities;
using Repositories.Interfaces;
using Repositories.Models.QueryModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Repositories
{
    public class AcccountProPackageRepository : GenericRepository<AccountProPackage>, IAccountProPackageRepository
    {
        public AcccountProPackageRepository(AppDbContext dbContext, IClaimsService claimsService) : base(dbContext, claimsService)
        {
        }
        public async Task<QueryResultModel<List<AccountProPackage>>> GetAllAsync(
            Expression<Func<AccountProPackage, bool>>? filter = null,
            int pageIndex = 1,
            int pageSize = 10,
            Func<IQueryable<AccountProPackage>, IIncludableQueryable<AccountProPackage, object>>? include = null)
        {
            IQueryable<AccountProPackage> query = _dbSet.AsQueryable();

            if (include != null)
            {
                query = include(query);
            }

            if (filter != null)
            {
                query = query.Where(filter);
            }

            int totalCount = await query.CountAsync();
            var data = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new QueryResultModel<List<AccountProPackage>>(data, totalCount);
        }
    }
}
