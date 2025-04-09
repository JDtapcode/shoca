﻿using Repositories.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Interfaces
{
    public interface IArtworkImageRepository : IGenericRepository<ArtworkImage>
    {
        Task<List<ArtworkImage>> GetByCreatedByAsync(Guid createdBy);
    }
}
