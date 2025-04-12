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
    public interface IAccountProPackageRepository : IGenericRepository<AccountProPackage>
    {
        Task<QueryResultModel<List<AccountProPackage>>> GetAllAsync(
            Expression<Func<AccountProPackage, bool>>? filter = null,
            int pageIndex = 1,
            int pageSize = 10,
            Func<IQueryable<AccountProPackage>, IIncludableQueryable<AccountProPackage, object>>? include = null);
    }
}
