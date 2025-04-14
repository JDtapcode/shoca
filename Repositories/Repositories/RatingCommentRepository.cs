using Microsoft.EntityFrameworkCore;
using Repositories.Entities;
using Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Repositories
{
    public class RatingCommentRepository : GenericRepository<RatingComment>, IRatingCommentRepository
    {
        public RatingCommentRepository(AppDbContext dbContext, IClaimsService claimsService) : base(dbContext, claimsService)
        {
        }
        public async Task<RatingComment> GetAsync(Guid id, Func<IQueryable<RatingComment>, IQueryable<RatingComment>> include = null)
        {
            var query = _dbSet.AsQueryable();
            if (include != null)
            {
                query = include(query);
            }
            return await query.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public void HardDelete(RatingComment entity)
        {
            _dbSet.Remove(entity);
        }
        public async Task HardDeleteByIdsAsync(IEnumerable<Guid> commentIds)
        {
            var comments = await _dbSet
                .Where(c => commentIds.Contains(c.Id))
                .ToListAsync();

            if (comments.Any())
            {
                _dbSet.RemoveRange(comments);
            }
        }
    }
}
