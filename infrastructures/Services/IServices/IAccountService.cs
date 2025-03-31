﻿using RestaurantManagementSystem.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace infrastructures.Services.IServices
{
    public interface IAccountService
    {
        Task<object> RegisterAsync(ApplicationUserDto userDto);
        Task<object> LoginAsync(LoginDto userVm);
        Task<bool> ConfirmEmailAsync(string userId, string token);
        Task SendEmailAsync(string email, string subject, string htmlMessage);

    }
}
