using Repositories.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Interfaces
{
    public interface IRatingCommentRepository : IGenericRepository<RatingComment>
    {
        Task<RatingComment> GetAsync(Guid id, Func<IQueryable<RatingComment>, IQueryable<RatingComment>> include = null);
        void HardDelete(RatingComment entity);
        Task HardDeleteByIdsAsync(IEnumerable<Guid> commentIds);
    }
}
