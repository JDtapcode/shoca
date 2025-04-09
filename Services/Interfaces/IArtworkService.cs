﻿using Repositories.Models.ArtworkImageModels;
using Repositories.Models.ArtworkModels;
using Services.Common;
using Services.Models.ArtworkModels;
using Services.Models.ResponseModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interfaces
{
    public interface IArtworkService
    {
        Task<Pagination<ArtworkModel>> GetAllArtworkAsync(ArtworkFilterModel filterModel);
        Task<ResponseModel> CreateArtworkAsync(ArtworkCreateModel model);
        Task<ResponseModel> UpdateArtworkAsync(Guid id, ArtworkUpdateModel model);
        Task<ResponseModel> DeleteArtworkAsync(Guid id);
        Task<ResponseDataModel<ArtworkModel>> GetArtworkByIdAsync(Guid id);
        Task<ResponseModel> RestoreArtwork(Guid id);
        Task<ResponseList<List<ArtworkImageModel>>> GetArtworkImagesByCreatorAsync(Guid creatorId);
        Task<ResponseModel> UpdateArtworkStatusAsync(Guid id, ArtworkStatusUpdateModel model);
    }
}
