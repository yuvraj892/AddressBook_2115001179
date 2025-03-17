﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ModelLayer.DTO;
using RepositoryLayer.Context;
using RepositoryLayer.Entity;
using RepositoryLayer.Hashing;
using RepositoryLayer.Helper;
using RepositoryLayer.Interface;

namespace RepositoryLayer.Service
{
    public class UserRL : IUserRL
    {
        private readonly AddressBookContext _context;
        private readonly JwtHelper _jwtHelper;
        private readonly ResetTokenHelper _resetTokenHelper;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly RedisCacheHelper _cacheHelper;


        public UserRL(AddressBookContext context, JwtHelper jwtHelper, ResetTokenHelper resetTokenHelper, IConfiguration configuration, EmailService emailService, RedisCacheHelper cacheHelper)
        {
            _context = context;
            _jwtHelper = jwtHelper;
            _resetTokenHelper = resetTokenHelper;
            _configuration = configuration;
            _emailService = emailService;
            _cacheHelper = cacheHelper;
        }

        public UserDTO Register(UserDTO userDto)
        {
            var passwordHasher = new Password_Hash();
            string hashedPassword = passwordHasher.HashPassword(userDto.Password);

            var user = new UserEntry
            {
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Email = userDto.Email,
                PasswordHash = hashedPassword,
                Role = "User"
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return new UserDTO
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            };
        }

        public async Task<string> Login(LoginDTO loginDto)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == loginDto.Email);

            var passwordHasher = new Password_Hash();
            if (user == null || !passwordHasher.VerifyPassword(loginDto.Password, user.PasswordHash))
            {
                return null;
            }

            string token = _jwtHelper.GenerateToken(user.Email, user.Role);

            await _cacheHelper.SetCacheAsync($"user:{user.Email}", user);

            return token;
        }

        public int GetUserIdByEmail(string email)
        {
            var cachedUser = _cacheHelper.GetCacheAsync<UserEntry>($"user_{email}").Result;

            if (cachedUser != null)
            {
                return cachedUser.Id;
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                _cacheHelper.SetCacheAsync($"user_{user.Email}", user).Wait();
            }

            return user?.Id ?? 0;
        }

        public bool ForgotPassword(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                return false;
            }

            string token = _resetTokenHelper.GeneratePasswordResetToken(user.Id, user.Email);

            string baseUrl = _configuration["Application:BaseUrl"];

            bool emailSent = _emailService.SendPasswordResetEmail(email, token, baseUrl);

            return emailSent;

        }

        public bool ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                return false;
            }
            if (!_resetTokenHelper.ValidatePasswordResetToken(token, out string email))
            {
                return false;
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                return false;
            }

            var passwordHasher = new Password_Hash();
            string hashedPassword = passwordHasher.HashPassword(newPassword);

            user.PasswordHash = hashedPassword;

            _context.SaveChanges();

            _cacheHelper.RemoveCacheAsync($"user_{user.Email}").Wait();

            return true;
        }




    }
}
