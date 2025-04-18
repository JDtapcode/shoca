﻿using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repositories.Entities;
using Repositories.Interfaces;
using Repositories.Models.AccountModels;
using Repositories.Models.AccountProPackageModels;
using Repositories.Models.ProPackageModels;
using Repositories.Utils;
using Services.Common;
using Services.Interfaces;
using Services.Models.AccountModels;
using Services.Models.CommonModels;
using Services.Models.ResponseModels;
using Services.Models.TokenModels;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Services.Services
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<Account> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly IClaimsService _claimsService;

        public AccountService(UserManager<Account> userManager, IUnitOfWork unitOfWork, IMapper mapper,
            IConfiguration configuration, IEmailService emailService, IClaimsService claimsService)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _configuration = configuration;
            _emailService = emailService;
            _claimsService = claimsService;
        }

        public async Task<ResponseModel> Register(AccountRegisterModel accountRegisterModel)
        {
            // Check if email already exists
            var existedEmail = await _userManager.FindByEmailAsync(accountRegisterModel.Email);

            if (existedEmail != null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "Email already exists"
                };
            }

            // Create new account
            var user = _mapper.Map<Account>(accountRegisterModel);
            user.UserName = user.Email;
            user.CreationDate = DateTime.Now;
            user.VerificationCode = AuthenticationTools.GenerateVerificationCode(6);
            user.VerificationCodeExpiryTime = DateTime.Now.AddMinutes(15);

            var result = await _userManager.CreateAsync(user, accountRegisterModel.Password);

            if (result.Succeeded)
            {
                // Add role
                await _userManager.AddToRoleAsync(user, Repositories.Enums.Role.Customer.ToString());

                // Email verification (disable this function if users are not to verify their email)
                await SendVerificationEmail(user);

                return new ResponseModel
                {
                    Status = true,
                    Message = "Account has been created successfully, please verify your email",
                    EmailVerificationRequired = true
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Password must contain uppercase letters, numbers and special characters"
            };
        }
        private async Task SendVerificationEmail(Account account)
        {
            await _emailService.SendEmailAsync(account.Email!, "Verify your email",
                $"Your verification code is {account.VerificationCode}. The code will expire in 15 minutes.", true);
        }
        public async Task<ResponseDataModel<TokenModel>> Login(AccountLoginModel accountLoginModel)
        {
            var user = await _userManager.FindByNameAsync(accountLoginModel.Email);

            if (user != null)
            {
                if (user.IsDeleted)
                {
                    return new ResponseDataModel<TokenModel>
                    {
                        Status = false,
                        Message = "Account has been deleted"
                    };
                }

                if (await _userManager.CheckPasswordAsync(user, accountLoginModel.Password))
                {
                    var authClaims = new List<Claim>
            {
                new Claim("userId", user.Id.ToString()),
                new Claim("userEmail", user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

                    var userRoles = await _userManager.GetRolesAsync(user);

                    foreach (var userRole in userRoles)
                    {
                        authClaims.Add(new Claim(ClaimTypes.Role, userRole));
                    }

                    // Check if refresh token is expired, if so then update
                    if (user.RefreshToken == null || user.RefreshTokenExpiryTime < DateTime.Now)
                    {
                        var refreshToken = TokenTools.GenerateRefreshToken();
                        _ = int.TryParse(_configuration["JWT:RefreshTokenValidityInDays"],
                            out int refreshTokenValidityInDays);

                        // Update user's refresh token
                        user.RefreshToken = refreshToken;
                        user.RefreshTokenExpiryTime = DateTime.Now.AddDays(refreshTokenValidityInDays);

                        var result = await _userManager.UpdateAsync(user);

                        if (!result.Succeeded)
                        {
                            return new ResponseDataModel<TokenModel>
                            {
                                Status = false,
                                Message = "Cannot login"
                            };
                        }
                    }

                    var jwtToken = TokenTools.CreateJWTToken(authClaims, _configuration);

                    return new ResponseDataModel<TokenModel>
                    {
                        Status = true,
                        Message = "Login successfully",
                        EmailVerificationRequired = !user.EmailConfirmed,
                        Data = new TokenModel
                        {
                            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                            AccessTokenExpiryTime = jwtToken.ValidTo.ToLocalTime(),
                            RefreshToken = user.RefreshToken,
                            UserId = user.Id // Thêm UserId vào đây
                        }
                    };
                }
            }

            // Nếu không tìm thấy user hoặc mật khẩu sai, trả về thông báo lỗi
            return new ResponseDataModel<TokenModel>
            {
                Status = false,
                Message = "Invalid email or password"
            };
        }

       
        public async Task<ResponseDataModel<TokenModel>> RefreshToken(RefreshTokenModel refreshTokenModel)
        {
            // Validate access token and refresh token
            var principal = TokenTools.GetPrincipalFromExpiredToken(refreshTokenModel.AccessToken, _configuration);

            var user = await _userManager.FindByIdAsync(principal!.FindFirst("userId")!.Value);

            if (user == null || user.RefreshToken != refreshTokenModel.RefreshToken ||
                user.RefreshTokenExpiryTime <= DateTime.Now)
            {
                return new ResponseDataModel<TokenModel>
                {
                    Status = false,
                    Message = "Invalid access token or refresh token"
                };
            }

            // Start to refresh access token and refresh token
            var refreshToken = TokenTools.GenerateRefreshToken();
            _ = int.TryParse(_configuration["JWT:RefreshTokenValidityInDays"], out int refreshTokenValidityInDays);

            // Update user's refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(refreshTokenValidityInDays);

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return new ResponseDataModel<TokenModel>
                {
                    Status = false,
                    Message = "Cannot refresh the token"
                };
            }

            var jwtToken = TokenTools.CreateJWTToken(principal.Claims.ToList(), _configuration);

            return new ResponseDataModel<TokenModel>
            {
                Status = true,
                Message = "Refresh token successfully",
                Data = new TokenModel
                {
                    AccessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                    AccessTokenExpiryTime = jwtToken.ValidTo.ToLocalTime(),
                    RefreshToken = user.RefreshToken,
                }
            };
        }

        public async Task<ResponseModel> VerifyEmail(string email, string verificationCode)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "User not found"
                };
            }

            if (user.VerificationCodeExpiryTime < DateTime.Now)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "The code is expired",
                    EmailVerificationRequired = false
                };
            }

            if (user.VerificationCode == verificationCode)
            {
                user.EmailConfirmed = true;
                user.VerificationCode = null;
                user.VerificationCodeExpiryTime = null;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return new ResponseModel
                    {
                        Status = true,
                        Message = "Verify email successfully",
                    };
                }
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Cannot verify email",
            };
        }

        public async Task<ResponseModel> ResendVerificationEmail(EmailModel? emailModel)
        {
            var currentUserId = _claimsService.GetCurrentUserId;

            if (emailModel == null && currentUserId == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "User not found",
                };
            }

            Account? user = null;

            if (emailModel != null && currentUserId == null)
            {
                user = await _userManager.FindByEmailAsync(emailModel.Email);

                if (user == null)
                {
                    return new ResponseModel
                    {
                        Status = false,
                        Message = "User not found",
                    };
                }
            }
            else if (emailModel == null && currentUserId != null)
            {
                user = await _userManager.FindByIdAsync(currentUserId.ToString()!);
            }
            else if (emailModel != null && currentUserId != null)
            {
                user = await _userManager.FindByEmailAsync(emailModel.Email);

                if (user == null || user.Id != currentUserId)
                {
                    return new ResponseModel
                    {
                        Status = false,
                        Message = "Cannot resend verification email",
                        EmailVerificationRequired = true
                    };
                }
            }

            if (user!.EmailConfirmed)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "Email has been verified",
                };
            }

            // Update new verification code
            user.VerificationCode = AuthenticationTools.GenerateVerificationCode(6);
            user.VerificationCodeExpiryTime = DateTime.Now.AddMinutes(15);
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await SendVerificationEmail(user);

                return new ResponseModel
                {
                    Status = true,
                    Message = "Resend Verification email successfully",
                    EmailVerificationRequired = true
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Cannot resend verification email",
                EmailVerificationRequired = true
            };
        }

        public async Task<ResponseModel> ChangePassword(AccountChangePasswordModel accountChangePasswordModel)
        {
            var currentUserId = _claimsService.GetCurrentUserId;
            var user = await _userManager.FindByIdAsync(currentUserId.ToString()!);

            var result = await _userManager.ChangePasswordAsync(user!, accountChangePasswordModel.OldPassword,
                accountChangePasswordModel.NewPassword);

            if (result.Succeeded)
            {
                return new ResponseModel
                {
                    Status = true,
                    Message = "Change password successfully",
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Cannot change password",
            };
        }

        public async Task<ResponseModel> ForgotPassword(EmailModel emailModel)
        {
            var user = await _userManager.FindByEmailAsync(emailModel.Email);

            if (user == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "User not found",
                };
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // todo modify this Email body to send a URL redirect to the frontend page and contain the token as a parameter in the URL
            await _emailService.SendEmailAsync(user.Email!, "Reset your Password",
                $"Your token is {token}. The token will expire in 15 minutes.", true);

            return new ResponseModel
            {
                Status = true,
                Message = "An email has been sent, please check your inbox",
            };
        }

        public async Task<ResponseModel> ResetPassword(AccountResetPasswordModel accountResetPasswordModel)
        {
            var user = await _userManager.FindByEmailAsync(accountResetPasswordModel.Email);

            if (user == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "User not found",
                };
            }

            var result = await _userManager.ResetPasswordAsync(user, accountResetPasswordModel.Token,
                accountResetPasswordModel.Password);

            if (result.Succeeded)
            {
                return new ResponseModel
                {
                    Status = true,
                    Message = "Reset password successfully",
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Cannot reset password",
            };
        }
        //public async Task<ResponseModel> AddAccounts(AccountRegisterModel accountRegisterModels)
        //{
        //    int count = 0;

        //    // Check if email already exists
        //    var existedEmail = await _userManager.FindByEmailAsync(accountRegisterModels.Email);

        //    if (existedEmail == null)
        //    {
        //        // Create new account
        //        var user = _mapper.Map<Account>(accountRegisterModels);
        //        user.UserName = user.Email;
        //        user.CreationDate = DateTime.Now;
        //        // user.VerificationCode = AuthenticationTools.GenerateVerificationCode(6);
        //        // user.VerificationCodeExpiryTime = DateTime.Now.AddMinutes(15);

        //        var result = await _userManager.CreateAsync(user, accountRegisterModels.Password);

        //        if (result.Succeeded)
        //        {
        //            // Add role
        //            var role = accountRegisterModels.Role?.ToString() ?? Repositories.Enums.Role.Customer.ToString();
        //            await _userManager.AddToRoleAsync(user, role);

        //            // Email verification (disable this function if users are not to verify their email)
        //            // await SendVerificationEmail(user);

        //            count++;
        //        }
        //    }


        //    return new ResponseModel
        //    {
        //        Status = true,
        //        Message = $"Add {count} accounts successfully"
        //    };
        //}
        public async Task<ResponseModel> AddAccounts(AccountRegisterModel accountRegisterModel)
        {
            // Kiểm tra dữ liệu đầu vào
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(accountRegisterModel.FirstName))
                errors.Add("Họ không được để trống.");

            if (string.IsNullOrWhiteSpace(accountRegisterModel.LastName))
                errors.Add("Tên không được để trống.");

            if (string.IsNullOrWhiteSpace(accountRegisterModel.PhoneNumber))
                errors.Add("Số điện thoại không được để trống.");

            if (string.IsNullOrWhiteSpace(accountRegisterModel.Email))
                errors.Add("Email không được để trống.");
            else
            {
                // Kiểm tra định dạng email
                string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!Regex.IsMatch(accountRegisterModel.Email, emailPattern))
                {
                    errors.Add("Email không hợp lệ.");
                }
            }

            if (string.IsNullOrWhiteSpace(accountRegisterModel.Password))
                errors.Add("Mật khẩu không được để trống.");

            if (string.IsNullOrWhiteSpace(accountRegisterModel.ConfirmPassword))
                errors.Add("Xác nhận mật khẩu không được để trống.");

            if (accountRegisterModel.Password != accountRegisterModel.ConfirmPassword)
                errors.Add("Mật khẩu xác nhận không khớp.");

            if (string.IsNullOrWhiteSpace(accountRegisterModel.Address))
                errors.Add("Địa chỉ không được để trống.");

            if (accountRegisterModel.DateOfBirth == default || accountRegisterModel.DateOfBirth.Year < 1900)
                errors.Add("Ngày sinh không hợp lệ. Vui lòng nhập đúng định dạng (yyyy-MM-dd).");

            if (errors.Any())
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "Vui lòng kiểm tra lại thông tin.",
                    Errors = errors
                };
            }

            // Kiểm tra email đã tồn tại chưa
            var existedEmail = await _userManager.FindByEmailAsync(accountRegisterModel.Email);

            if (existedEmail != null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "Email đã tồn tại trong hệ thống."
                };
            }

            // Tạo tài khoản mới
            var user = _mapper.Map<Account>(accountRegisterModel);
            user.UserName = user.Email;
            user.CreationDate = DateTime.Now;

            var result = await _userManager.CreateAsync(user, accountRegisterModel.Password);

            if (result.Succeeded)
            {
                // Thêm quyền (role)
                var role = accountRegisterModel.Role?.ToString() ?? Repositories.Enums.Role.Customer.ToString();
                await _userManager.AddToRoleAsync(user, role);

                return new ResponseModel
                {
                    Status = true,
                    Message = "Tài khoản đã được tạo thành công."
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Mật khẩu phải chứa chữ hoa, số và ký tự đặc biệt."
            };
        }
        //public async Task<ResponseDataModel<AccountModel>> GetAccount(Guid id)
        //{
        //    var user = await _userManager.FindByIdAsync(id.ToString());
        //    if (user == null)
        //    {
        //        return new ResponseDataModel<AccountModel>()
        //        {
        //            Status = false,
        //            Message = "User not found"
        //        };
        //    }

        //    var accountProPackages = await _unitOfWork.AccountProPackageRepository
        //        .GetAllAsync(app => app.AccountId == id && !app.IsDeleted);

        //    var userModel = _mapper.Map<AccountModel>(user);

        //    userModel.PurchasedPackages = accountProPackages.Data.Select(app => new AccountProPackageInfo
        //    {
        //        Id = app.Id,
        //        ProPackageId = app.ProPackageId,
        //        StartDate = app.StartDate,
        //        EndDate = app.EndDate,
        //        PackageStatus = app.PackageStatus.ToString()
        //    }).ToList();

        //    var role = await _userManager.GetRolesAsync(user);
        //    userModel.Role = Enum.Parse(typeof(Repositories.Enums.Role), role[0]).ToString();

        //    return new ResponseDataModel<AccountModel>()
        //    {
        //        Status = true,
        //        Message = "Get account successfully",
        //        Data = userModel
        //    };
        //}


        public async Task<ResponseDataModel<AccountModel>> GetAccount(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new ResponseDataModel<AccountModel>()
                {
                    Status = false,
                    Message = "Không tìm thấy người dùng"
                };
            }

            var accountProPackages = await _unitOfWork.AccountProPackageRepository
                .GetAllAsync(
                    app => app.AccountId == id && !app.IsDeleted,
                    include: q => q.Include(app => app.ProPackage).ThenInclude(pp => pp.Features));

            var userModel = _mapper.Map<AccountModel>(user);

            userModel.PurchasedPackages = accountProPackages.Data.Select(app => new AccountProPackageInfos
            {
                Id = app.Id,
                ProPackageId = app.ProPackageId,
                StartDate = app.StartDate,
                EndDate = app.EndDate,
                PackageStatus = app.PackageStatus.ToString(),
                ProPackage = app.ProPackage != null ? new ProPackageDTO
                {
                    Id = app.ProPackage.Id,
                    Name = app.ProPackage.Name,
                    Price = app.ProPackage.Price,
                    Duration = app.ProPackage.Duration,
                    FeatureNames = app.ProPackage.Features?.Select(f => f.Name).ToList() ?? new List<string>()
                } : null
            }).ToList();

            var role = await _userManager.GetRolesAsync(user);
            userModel.Role = Enum.Parse(typeof(Repositories.Enums.Role), role[0]).ToString();

            return new ResponseDataModel<AccountModel>()
            {
                Status = true,
                Message = "Lấy thông tin tài khoản thành công",
                Data = userModel
            };
        }

        //    public async Task<Pagination<AccountModel>> GetAllAccounts(AccountFilterModel accountFilterModel)
        //    {
        //        var accountList = await _unitOfWork.AccountRepository.GetAllAsync(
        //            pageIndex: accountFilterModel.PageIndex,
        //            pageSize: accountFilterModel.PageSize,
        //            filter: (x =>
        //                x.IsDeleted == accountFilterModel.IsDeleted &&
        //                (accountFilterModel.Gender == null || x.Gender == accountFilterModel.Gender) &&
        //                (accountFilterModel.Role == null || x.Role == accountFilterModel.Role.ToString()) &&
        //                (string.IsNullOrEmpty(accountFilterModel.Search) ||
        //                 x.FirstName!.ToLower().Contains(accountFilterModel.Search.ToLower()) ||
        //                 x.LastName!.ToLower().Contains(accountFilterModel.Search.ToLower()) ||
        //                 x.Email!.ToLower().Contains(accountFilterModel.Search.ToLower()))),
        //            orderBy: (x =>
        //            {
        //                switch (accountFilterModel.Order.ToLower())
        //                {
        //                    case "first-name":
        //                        return accountFilterModel.OrderByDescending
        //                            ? x.OrderByDescending(x => x.FirstName)
        //                            : x.OrderBy(x => x.FirstName);
        //                    case "last-name":
        //                        return accountFilterModel.OrderByDescending
        //                            ? x.OrderByDescending(x => x.LastName)
        //                            : x.OrderBy(x => x.LastName);
        //                    case "date-of-birth":
        //                        return accountFilterModel.OrderByDescending
        //                            ? x.OrderByDescending(x => x.DateOfBirth)
        //                            : x.OrderBy(x => x.DateOfBirth);
        //                    default:
        //                        return accountFilterModel.OrderByDescending
        //                            ? x.OrderByDescending(x => x.CreationDate)
        //                            : x.OrderBy(x => x.CreationDate);
        //                }
        //            })
        //        );

        //        var accountModelList = _mapper.Map<List<AccountModel>>(accountList.Data);

        //        // Truy vấn PurchasedPackages cho tất cả tài khoản
        //        foreach (var accountModel in accountModelList)
        //        {
        //            var accountProPackages = await _unitOfWork.AccountProPackageRepository
        //.GetAllAsync(app => app.AccountId == accountModel.Id && !app.IsDeleted);

        //            accountModel.PurchasedPackages = accountProPackages.Data.Select(app => new AccountProPackageInfo
        //            {
        //                Id = app.Id,
        //                ProPackageId = app.ProPackageId,
        //                StartDate = app.StartDate,
        //                EndDate = app.EndDate,
        //                PackageStatus = app.PackageStatus.ToString()
        //            }).ToList();
        //        }

        //        return new Pagination<AccountModel>(accountModelList, accountList.TotalCount,
        //            accountFilterModel.PageIndex,
        //            accountFilterModel.PageSize);
        //    }
        public async Task<Pagination<AccountModel>> GetAllAccounts(AccountFilterModel accountFilterModel)
        {
            var accountList = await _unitOfWork.AccountRepository.GetAllAsync(
                pageIndex: accountFilterModel.PageIndex,
                pageSize: accountFilterModel.PageSize,
                filter: (x =>
                    x.IsDeleted == accountFilterModel.IsDeleted &&
                    (accountFilterModel.Gender == null || x.Gender == accountFilterModel.Gender) &&
                    (accountFilterModel.Role == null || x.Role == accountFilterModel.Role.ToString()) &&
                    (string.IsNullOrEmpty(accountFilterModel.Search) ||
                     x.FirstName!.ToLower().Contains(accountFilterModel.Search.ToLower()) ||
                     x.LastName!.ToLower().Contains(accountFilterModel.Search.ToLower()) ||
                     x.Email!.ToLower().Contains(accountFilterModel.Search.ToLower()))),
                orderBy: (x =>
                {
                    switch (accountFilterModel.Order.ToLower())
                    {
                        case "first-name":
                            return accountFilterModel.OrderByDescending
                                ? x.OrderByDescending(x => x.FirstName)
                                : x.OrderBy(x => x.FirstName);
                        case "last-name":
                            return accountFilterModel.OrderByDescending
                                ? x.OrderByDescending(x => x.LastName)
                                : x.OrderBy(x => x.LastName);
                        case "date-of-birth":
                            return accountFilterModel.OrderByDescending
                                ? x.OrderByDescending(x => x.DateOfBirth)
                                : x.OrderBy(x => x.DateOfBirth);
                        default:
                            return accountFilterModel.OrderByDescending
                                ? x.OrderByDescending(x => x.CreationDate)
                                : x.OrderBy(x => x.CreationDate);
                    }
                })
            );

            var accountModelList = _mapper.Map<List<AccountModel>>(accountList.Data);

            // Lấy tất cả AccountProPackage cho các tài khoản trong danh sách
            var accountIds = accountModelList.Select(a => a.Id).ToList();
            var accountProPackages = await _unitOfWork.AccountProPackageRepository
                .GetAllAsync(app => accountIds.Contains(app.AccountId) && !app.IsDeleted);

            // Gán PurchasedPackages cho từng tài khoản
            foreach (var accountModel in accountModelList)
            {
                accountModel.PurchasedPackages = accountProPackages.Data
                    .Where(app => app.AccountId == accountModel.Id)
                    .Select(app => new AccountProPackageInfos
                    {
                        Id = app.Id,
                        ProPackageId = app.ProPackageId,
                        StartDate = app.StartDate,
                        EndDate = app.EndDate,
                        PackageStatus = app.PackageStatus.ToString(),
                        ProPackage = null // Không trả về chi tiết ProPackage
                    }).ToList();
            }

            return new Pagination<AccountModel>(
                accountModelList,
                accountList.TotalCount,
                accountFilterModel.PageIndex,
                accountFilterModel.PageSize);
        }
        public async Task<ResponseDataModel<AccountUpdateModel>> UpdateAccount(Guid id, AccountUpdateModel accountUpdateModel)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                return new ResponseDataModel<AccountUpdateModel>
                {
                    Status = false,
                    Message = "User not found",
                    Data = null
                };
            }

            // Cập nhật các thuộc tính khác
            user.FirstName = accountUpdateModel.FirstName;
            user.LastName = accountUpdateModel.LastName;
            user.Gender = accountUpdateModel.Gender;
            user.DateOfBirth = accountUpdateModel.DateOfBirth;
            user.Address = accountUpdateModel.Address;
            user.AvatarUrl = accountUpdateModel.AvatarUrl;
            user.PersonalWebsiteUrl = accountUpdateModel.PersonalWebsiteUrl;
            user.PortfolioUrl = accountUpdateModel.PortfolioUrl;
            user.PhoneNumber = accountUpdateModel.PhoneNumber;
            user.ModificationDate = DateTime.Now;
            user.ModifiedBy = _claimsService.GetCurrentUserId;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                return new ResponseDataModel<AccountUpdateModel>
                {
                    Status = false,
                    Message = "Cannot update account",
                    Data = accountUpdateModel
                };
            }

            // Quản lý vai trò người dùng - Chỉ cập nhật nếu role thay đổi
            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault();
            var newRole = accountUpdateModel.Role.ToString();

            if (!string.IsNullOrEmpty(newRole) && currentRole != newRole)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                var addRoleResult = await _userManager.AddToRoleAsync(user, newRole);

                if (!addRoleResult.Succeeded)
                {
                    return new ResponseDataModel<AccountUpdateModel>
                    {
                        Status = false,
                        Message = "Cannot update account role",
                        Data = accountUpdateModel
                    };
                }
            }

            return new ResponseDataModel<AccountUpdateModel>
            {
                Status = true,
                Message = "Update account successfully",
                Data = accountUpdateModel
            };
        }
        //public async Task<ResponseDataModel<AccountUpdateModel>> UpdateAccount(Guid id, AccountUpdateModel accountUpdateModel)
        //{
        //    var user = await _userManager.FindByIdAsync(id.ToString());

        //    if (user == null)
        //    {
        //        return new ResponseDataModel<AccountUpdateModel>
        //        {
        //            Status = false,
        //            Message = "User not found",
        //            Data = null
        //        };
        //    }

        //    user.FirstName = accountUpdateModel.FirstName;
        //    user.LastName = accountUpdateModel.LastName;
        //    user.Gender = accountUpdateModel.Gender;
        //    user.DateOfBirth = accountUpdateModel.DateOfBirth;
        //    user.Address = accountUpdateModel.Address;
        //    user.AvatarUrl = accountUpdateModel.AvatarUrl;
        //    user.PersonalWebsiteUrl = accountUpdateModel.PersonalWebsiteUrl;
        //    user.PortfolioUrl = accountUpdateModel.PortfolioUrl;
        //    user.PhoneNumber = accountUpdateModel.PhoneNumber;
        //    user.ModificationDate = DateTime.Now;
        //    user.ModifiedBy = _claimsService.GetCurrentUserId;

        //    var updateResult = await _userManager.UpdateAsync(user);

        //    if (!updateResult.Succeeded)
        //    {
        //        return new ResponseDataModel<AccountUpdateModel>
        //        {
        //            Status = false,
        //            Message = "Cannot update account",
        //            Data = accountUpdateModel
        //        };
        //    }

        //    // Quản lý vai trò người dùng
        //    var currentRoles = await _userManager.GetRolesAsync(user);
        //    await _userManager.RemoveFromRolesAsync(user, currentRoles);
        //    var addRoleResult = await _userManager.AddToRoleAsync(user, accountUpdateModel.Role.ToString());

        //    if (!addRoleResult.Succeeded)
        //    {
        //        return new ResponseDataModel<AccountUpdateModel>
        //        {
        //            Status = false,
        //            Message = "Cannot update account role",
        //            Data = accountUpdateModel
        //        };
        //    }

        //    return new ResponseDataModel<AccountUpdateModel>
        //    {
        //        Status = true,
        //        Message = "Update account and role successfully",
        //        Data = accountUpdateModel
        //    };
        //}



        public async Task<ResponseModel> DeleteAccount(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "User not found"
                };
            }

            user.IsDeleted = true;
            user.DeletionDate = DateTime.Now;
            user.DeletedBy = _claimsService.GetCurrentUserId;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return new ResponseModel
                {
                    Status = true,
                    Message = "Delete account successfully",
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Cannot delete account",
            };
        }

        public async Task<ResponseModel> RestoreAccount(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "User not found"
                };
            }

            user.IsDeleted = false;
            user.DeletionDate = null;
            user.DeletedBy = null;
            user.ModificationDate = DateTime.UtcNow;
            user.ModifiedBy = _claimsService.GetCurrentUserId;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return new ResponseModel
                {
                    Status = true,
                    Message = "Restore account successfully",
                };
            }

            return new ResponseModel
            {
                Status = false,
                Message = "Cannot restore account",
            };
        }
    }
}
