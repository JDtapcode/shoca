using Microsoft.EntityFrameworkCore.Query;
using Repositories.Entities;
using Repositories.Models.QueryModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Interfaces
{
    public interface IRatingRepository : IGenericRepository<Rating>
    {
        Task<Rating> GetAsync(Guid id, Func<IQueryable<Rating>, IIncludableQueryable<Rating, object>>? include = null);
        Task<QueryResultModel<List<Rating>>> GetAllAsync(
            Expression<Func<Rating, bool>>? filter = null,
            int pageIndex = 1,
            int pageSize = 10,
            Func<IQueryable<Rating>, IIncludableQueryable<Rating, object>>? include = null);
    }
}
