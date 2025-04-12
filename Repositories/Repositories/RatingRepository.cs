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
    public class RatingRepository : GenericRepository<Rating>, IRatingRepository
    {
        public RatingRepository(AppDbContext dbContext, IClaimsService claimsService) : base(dbContext, claimsService)
        {
        }
        public async Task<Rating> GetAsync(Guid id, Func<IQueryable<Rating>, IIncludableQueryable<Rating, object>>? include = null)
        {
            IQueryable<Rating> query = _dbSet.AsQueryable();

            if (include != null)
            {
                query = include(query);
            }

            return await query.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }
        public async Task<QueryResultModel<List<Rating>>> GetAllAsync(
            Expression<Func<Rating, bool>>? filter = null,
            int pageIndex = 1,
            int pageSize = 10,
            Func<IQueryable<Rating>, IIncludableQueryable<Rating, object>>? include = null)
        {
            IQueryable<Rating> query = _dbSet.AsQueryable();

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

            return new QueryResultModel<List<Rating>>(data, totalCount);
        }
    }
}
