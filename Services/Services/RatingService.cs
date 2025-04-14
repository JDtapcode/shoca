using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Repositories.Entities;
using Repositories.Interfaces;
using Repositories.Models.RatingModels;
using Services.Common;
using Services.Interfaces;
using Services.Models.RatingModels;
using Services.Models.ResponseModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    public class RatingService : IRatingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public RatingService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task<ResponseModel> AddComment(RatingCommentCreateModel ratingCommentCreateModel)
        {
            if (ratingCommentCreateModel == null)
            {
                return new ResponseModel { Status = false, Message = "Rating not found." };
            }
            var rating = await _unitOfWork.RatingRepository.GetAsync(ratingCommentCreateModel.RatingId);
            if (rating == null)
            {
                return new ResponseModel { Status = false, Message = "Rating not found." };
            }

            var comment = new RatingComment
            {
                RatingId = ratingCommentCreateModel.RatingId,
                CommentText = ratingCommentCreateModel.CommentText,
                AccountId = ratingCommentCreateModel.AccountId,
                ParentCommentId = ratingCommentCreateModel.ParentCommentId
            };

            await _unitOfWork.CommentRepository.AddAsync(comment);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Comment added successfully." };
        }

        public async Task<ResponseModel> CreateRating(RatingCreateModel ratingCreateModel)
        {
            var rating = new Rating
            {
                ArtworkId = ratingCreateModel.ArtworkId,
                RatingValue = ratingCreateModel.RatingValue,
                Comments = ratingCreateModel.Comments,
                CustomerId = ratingCreateModel.CustomerId,
            };

            await _unitOfWork.RatingRepository.AddAsync(rating);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Rating created successfully." };
        }
        public async Task<ResponseDataModel<RatingModel>> GetRatingById(Guid ratingId)
        {
            var rating = await _unitOfWork.RatingRepository.GetAsync(
                ratingId,
                include: q => q
                    .Include(r => r.Customer)
                    .Include(r => r.Artwork)
                    .Include(r => r.CommentsList)
                        .ThenInclude(c => c.Account)
            );

            if (rating == null || rating.IsDeleted == true)
            {
                return new ResponseDataModel<RatingModel>
                {
                    Status = false,
                    Message = "Rating is not found"
                };
            }

            var ratingModel = _mapper.Map<RatingModel>(rating);
            return new ResponseDataModel<RatingModel> { Data = ratingModel, Status = true };
        }
        public async Task<Pagination<RatingModel>> GetRatingsByArtworkAsync(RatingFilterModel model)
        {
            var queryResult = await _unitOfWork.RatingRepository.GetAllAsync(
                filter: r => (r.IsDeleted == model.isDelete) &&
                             (model.ArtworkId == null || r.ArtworkId == model.ArtworkId) &&
                             (model.AccountId == null || r.CustomerId == model.AccountId) &&
                             (model.RatingValue == null || r.RatingValue == model.RatingValue),
                pageIndex: model.PageIndex,
                pageSize: model.PageSize,
                include: q => q
                    .Include(r => r.Customer)
                    .Include(r => r.Artwork)
                    .Include(r => r.CommentsList)
                        .ThenInclude(c => c.Account)
            );

            var ratings = _mapper.Map<List<RatingModel>>(queryResult.Data);
            return new Pagination<RatingModel>(ratings, model.PageIndex, model.PageSize, queryResult.TotalCount);
        }

        public async Task<ResponseModel> HardDeleteRatingAsync(Guid ratingId)
        {
            var rating = await _unitOfWork.RatingRepository.GetAsync(ratingId);

            if (rating == null)
            {
                return new ResponseModel { Status = false, Message = "Rating not found." };
            }
            _unitOfWork.RatingRepository.HardDelete(rating);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Rating and related comments deleted successfully." };
        }

        public async Task<ResponseModel> DeleteCommentAsync(Guid commentId)
        {
            // Lấy comment theo ID, bao gồm các comment con
            var comment = await _unitOfWork.CommentRepository.GetAsync(
                commentId,
                include: q => q.Include(c => c.ChildComments)
            );

            if (comment == null)
            {
                return new ResponseModel { Status = false, Message = "Comment not found." };
            }

            // Xóa đệ quy các comment con
            await HardDeleteChildCommentsAsync(comment.ChildComments);

            // Xóa comment chính
            _unitOfWork.CommentRepository.HardDelete(comment);

            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Comment and its child comments deleted successfully." };
        }

        private async Task HardDeleteChildCommentsAsync(ICollection<RatingComment> childComments)
        {
            if (childComments == null || !childComments.Any())
                return;

            foreach (var childComment in childComments)
            {
                // Xóa đệ quy các comment con của comment hiện tại
                await HardDeleteChildCommentsAsync(childComment.ChildComments);
                _unitOfWork.CommentRepository.HardDelete(childComment);
            }
        }
        public async Task<ResponseModel> DeleteAsync(Guid id)
        {
            // Kiểm tra xem id có phải là Rating không
            var rating = await _unitOfWork.RatingRepository.GetAsync(
                id,
                include: q => q.Include(r => r.CommentsList)
            );

            if (rating != null)
            {
                // Xóa rating và các comment liên quan
                var commentIds = rating.CommentsList?.Select(c => c.Id).ToList() ?? new List<Guid>();
                if (commentIds.Any())
                {
                    await _unitOfWork.CommentRepository.HardDeleteByIdsAsync(commentIds);
                }

                _unitOfWork.RatingRepository.HardDelete(rating);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseModel { Status = true, Message = "Rating and related comments deleted successfully." };
            }

            // Nếu không phải Rating, kiểm tra xem có phải Comment không
            var comment = await _unitOfWork.CommentRepository.GetAsync(
                id,
                include: q => q.Include(c => c.ChildComments)
            );

            if (comment != null)
            {
                // Xóa comment và các comment con
                var commentIds = new List<Guid> { comment.Id };
                CollectChildCommentIds(comment.ChildComments, commentIds);

                await _unitOfWork.CommentRepository.HardDeleteByIdsAsync(commentIds);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseModel { Status = true, Message = "Comment and its child comments deleted successfully." };
            }

            // Nếu không tìm thấy cả Rating lẫn Comment
            return new ResponseModel { Status = false, Message = "No rating or comment found with the provided ID." };
        }

        private void CollectChildCommentIds(ICollection<RatingComment> childComments, List<Guid> commentIds)
        {
            if (childComments == null || !childComments.Any())
                return;

            foreach (var childComment in childComments)
            {
                commentIds.Add(childComment.Id);
                CollectChildCommentIds(childComment.ChildComments, commentIds);
            }
        }
    }
}
