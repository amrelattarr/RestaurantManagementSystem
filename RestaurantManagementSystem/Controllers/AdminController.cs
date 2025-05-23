﻿using infrastructures.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Utility;
using System.Threading.Tasks;
using Utility.SignalR;


[Route("api/admin")]
[ApiController]
[Authorize(Roles = SD.adminRole)]
public class AdminController : ControllerBase
{
    private readonly IFoodCategoryService _foodCategoryService;
    private readonly IRestaurantService _restaurantService;
    private readonly IOrderService _orderService;
    private readonly IReservationService _reservationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<AdminHub> _hubContext;
    private readonly IReviewService _reviewService;

    public AdminController(
        IFoodCategoryService foodCategoryService,
        IRestaurantService restaurantService,
        IOrderService orderService,
        IReservationService reservationService,
        UserManager<ApplicationUser> userManager,
        IHubContext<AdminHub> hubContext,
        IReviewService reviewService)
    {
        _foodCategoryService = foodCategoryService;
        _restaurantService = restaurantService;
        _orderService = orderService;
        _reservationService = reservationService;
        _userManager = userManager;
        _hubContext = hubContext;
        this._reviewService = reviewService;
    }
    [HttpGet("GetAllUsers")]
    public async Task<IActionResult> GetAllUsers(string? search, int pageNumber = 1)
    {
        int pageSize = 15;
        try
        {
            var query = _userManager.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Email.Contains(search));
            }
            var totalUsers = await query.CountAsync();
            var users = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var result = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                result.Add(new
                {
                    user.Id,
                    user.Email,
                    Roles = roles
                });
            }
            return Ok(new
            {
                TotalCount = totalUsers,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Users = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = "An error occurred.", Error = ex.Message });
        }
    }



    [HttpPut("AddRestaurantManager")]
    public async Task<IActionResult> AddRestaurantManager([FromForm] string Email)
    {
        if (string.IsNullOrWhiteSpace(Email))
            return BadRequest("Email is required.");

        try
        {

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
                return NotFound($"User with email {Email} not found.");

            if (await _userManager.IsInRoleAsync(user, "RestaurantManager"))
                return BadRequest("User is already a Restaurant Manager.");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            var roleResult = await _userManager.AddToRoleAsync(user, SD.RestaurantManagerRole);
            if (!roleResult.Succeeded)
                return StatusCode(500, "Failed to assign role.");

            return Ok(new
            {
                Success = true,
                Message = "User role updated to Restaurant Manager.",
                User = new { user.Id, user.Email, user.UserName }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while updating the user role.",
                Error = ex.Message
            });
        }
    }
    [HttpDelete("DeleteUser")]
    public async Task<IActionResult> DeleteUser([FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email is required.");

        try
        {
            // Find user by email
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return NotFound($"User with email {email} not found.");

            // Delete user
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return StatusCode(500, "Failed to delete user.");

            return Ok(new { Success = true, Message = $"User {email} deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = "An error occurred.", Error = ex.Message });
        }
    }
    [HttpPut("lock-user")]
    public async Task<IActionResult> LockUser([FromForm] string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(30));
        return Ok($"✅ User {user.Email} has been locked.");
    }

    [HttpPut("unlock-user")]
    public async Task<IActionResult> UnlockUser([FromForm] string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        await _userManager.SetLockoutEndDateAsync(user, null); // Remove lockout
        await _userManager.ResetAccessFailedCountAsync(user); // Reset failed login attempts
        return Ok($"✅ User {user.Email} has been unlocked.");
    }

    // ------------------------ Restaurant Management ------------------------
    
    [HttpGet("GetPendngRestaurants")]
    public async Task<IActionResult> GetPendingRestaurant([FromQuery] string? search, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var restaurants = await _restaurantService.GetAllRestaurantsAsync();
            var approvedRestaurants = restaurants.Where(r => r.Status == RestaurantStatus.Pending);

            if (!string.IsNullOrEmpty(search))
            {
                approvedRestaurants = approvedRestaurants.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var totalRecords = approvedRestaurants.Count();
            var pagedRestaurants = approvedRestaurants
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Restaurants = pagedRestaurants
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while retrieving the restaurant.", Error = ex.Message });
        }
    }

    [HttpGet("GetRestaurantDetails")]
    public async Task<IActionResult> GetRestaurantDetails(int RestaurantId)
    {
        try
        {
            var restaurant = await _restaurantService.GetRestaurantByIdAsync(RestaurantId);
            if (restaurant == null)
            {
                return NotFound(new { Message = $"❌ Restaurant with ID {RestaurantId} not found." });
            }

            return Ok(restaurant);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "❌ An error occurred while retrieving the restaurant details.",
                Error = ex.Message
            });
        }
    }

    [HttpGet("GetAllRestaurants")]
    public async Task<IActionResult> GetAllRestaurants([FromQuery] int page = 1, [FromQuery] string searchQuery = "")
    {
        try
        {
            int pageSize = 10;
            var restaurants = await _restaurantService.GetAllRestaurantsAsync();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                restaurants = restaurants.Where(r => r.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                                     r.Location.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            int totalCount = restaurants.Count();
            var paginatedRestaurants = restaurants.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new { TotalCount = totalCount, Page = page, PageSize = pageSize, Data = paginatedRestaurants });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }

    [HttpGet("GetAdminRestaurants")]
    public async Task<IActionResult> GetAdminRestaurants([FromQuery] int page = 1, [FromQuery] string searchQuery = "")
    {
        try
        {
            int pageSize = 10;
            var userManagerId = _userManager.GetUserId(User);
            if (userManagerId == null)
                return Unauthorized(new { Message = "Unauthorized access." });
            var restaurants = await _restaurantService.GetAllRestaurantsAsync(userManagerId);

            if (!string.IsNullOrEmpty(searchQuery))
            {
                restaurants = restaurants.Where(r => r.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                                     r.Location.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            int totalCount = restaurants.Count();
            var paginatedRestaurants = restaurants.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new { TotalCount = totalCount, Page = page, PageSize = pageSize, Data = paginatedRestaurants });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }

    [HttpPost("CreateAdminRestaurant")]
    public async Task<IActionResult> CreateAdminRestaurant([FromForm] string email, [FromForm] Models.Models.Restaurant restaurant, IFormFile? RestImg)
    {
        var user = await _userManager.FindByEmailAsync(email);
        try
        {
            
            if (user == null)
                return NotFound(new { Message = $"❌ User with email '{email}' not found." });

            restaurant.ManagerID = user.Id;
            restaurant.Status = (RestaurantStatus)ReservationStatus.Confirmed;

            await _restaurantService.CreateRestaurantAsync(restaurant, RestImg);
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "RestaurantAdded", restaurant);

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }
    [HttpPut("UpdateAdminRestaurant")]
    public async Task<IActionResult> UpdateAdminRestaurant(
        [FromForm] string email,
        int restaurantId,
        [FromForm] Models.Models.Restaurant restaurant,
        IFormFile? RestImg)
    {
        try
        {
            var existingRestaurant = await _restaurantService.GetRestaurantByIdAsync(restaurantId);
            if (existingRestaurant == null)
                return NotFound(new { Message = $"❌ Restaurant with ID {restaurantId} not found." });

            if (!string.IsNullOrWhiteSpace(email))
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound(new { Message = $"❌ User with email '{email}' not found." });
                restaurant.ManagerID = user.Id;
            }
            else
            {
                restaurant.ManagerID = existingRestaurant.ManagerID;
            }

            await _restaurantService.UpdateRestaurantAsync(restaurantId, restaurant, RestImg);
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "RestaurantUpdated", restaurant);

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "❌ An error occurred while updating the restaurant.", Error = ex.Message });
        }
    }

    [HttpDelete("DeleteRestaurant/{restaurantId}")]
    public async Task<IActionResult> DeleteRestaurant(int restaurantId)
    {
        try
        {
            await _restaurantService.DeleteRestaurantAsync(restaurantId);
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "RestaurantDeleted", restaurantId);
            return NoContent();
        }
        catch (Exception ex)
        {
            var detailedMessage = "❌ Error deleting restaurant. " +
                "Please make sure to remove all related entities such as reservations, tables, food categories, menu items, and time slots before deleting the restaurant.\n\n" +
                $"Technical Details: {ex.Message}";

            return StatusCode(500, detailedMessage);
        }
    }


    [HttpPut("restaurants/{restaurantId}/approve")]
    public async Task<IActionResult> ApproveRestaurantAsync(int restaurantId)
    {
        try
        {
            await _restaurantService.ApproveRestaurantAsync(restaurantId);
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
            {
                RestaurantId = restaurantId,
                Message = $"✅ Restaurant with ID {restaurantId} has been approved."
            });

            return Ok("✅ Restaurant approved.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }

    [HttpPut("restaurants/{restaurantId}/reject")]
    public async Task<IActionResult> RejectRestaurant(int restaurantId)
    {
        try
        {
            await _restaurantService.RejectRestaurantAsync(restaurantId);
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
            {
                RestaurantId = restaurantId,
                Message = $"❌ Restaurant with ID {restaurantId} has been rejected."
            });

            return Ok("❌ Restaurant rejected.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }
    // ------------------------ Food Category Management ------------------------

    [HttpGet("GetAllFoodCategoriesAsync")]
    public async Task<IActionResult> GetAllFoodCategoriesAsync(
     [FromQuery] int restaurantId,
     [FromQuery] int page = 1,
     [FromQuery] string searchQuery = "")
    {
        try
        {
            if (restaurantId==0)
                return BadRequest(new { Message = "❌ RestaurantId is required." });

            int pageSize = 10;
            var categories = await _foodCategoryService.GetAllCategoriesAsync(restaurantId);
            

            if (!string.IsNullOrEmpty(searchQuery))
            {
                categories = categories.Where(c => c.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            int totalCount = categories.Count();
            var paginatedCategories = categories
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = paginatedCategories
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }

    [HttpPost("AddFoodCategory")]
    public async Task<IActionResult> AddFoodCategory(
        [FromBody] Models.Models.FoodCategory category,
        [FromQuery] int restaurantId)
    {
        if (category == null)
            return BadRequest(new { Success = false, Message = "Invalid category data." });

        try
        {
            if (restaurantId==0)
                return BadRequest(new { Success = false, Message = "❌ RestaurantId is required." });

            category.RestaurantId = restaurantId;

            await _foodCategoryService.CreateCategoryAsync(category);
            await _hubContext.Clients.All.SendAsync("CategoryAdded", category);

            return Ok(new
            {
                Success = true,
                Message = "Category created successfully",
                Category = category
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpPut("UpdateFoodCategory/{categoryId}")]
    public async Task<IActionResult> UpdateFoodCategory(
        int categoryId,
        [FromBody] Models.Models.FoodCategory category,
        [FromQuery] int restaurantId)
    {
        if (category == null || category.CategoryID != categoryId)
            return BadRequest(new { Success = false, Message = "Invalid category ID." });

        try
        {
            if (restaurantId!=0)
                category.RestaurantId = restaurantId;

            await _foodCategoryService.UpdateCategoryAsync(categoryId, category);
            await _hubContext.Clients.All.SendAsync("CategoryUpdated", category);

            return Ok(new
            {
                Success = true,
                Message = "Category updated successfully",
                Category = category
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpDelete("DeleteFoodCategory/{categoryId}")]
    public async Task<IActionResult> DeleteFoodCategory(int categoryId)
    {
        if (categoryId <= 0)
            return BadRequest(new { Success = false, Message = "Invalid category ID." });

        try
        {
            var category = await _foodCategoryService.GetCategoryByIdAsync(categoryId);
            if (category == null)
                return NotFound(new { Success = false, Message = "Category not found." });

            await _foodCategoryService.DeleteCategoryAsync(categoryId);
            await _hubContext.Clients.All.SendAsync("CategoryDeleted", categoryId);

            return Ok(new { Success = true, Message = "Category deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpGet("GetAllOrders")]
    public async Task<IActionResult> GetAllOrders(int RestaurantId, [FromQuery] Models.Models.OrderStatus? status, [FromQuery] int page = 1)
    {
        try
        {
            int pageSize = 10;

            var orders = await _orderService.GetAllOrdersAsync(RestaurantId);
  
            if (status.HasValue)
            {
                orders = orders.Where(r => r.Status == status.Value).ToList();
            }

            int totalCount = orders.Count();

            var paginatedOrders = orders
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(res => new
                {
                    OrderID = res.OrderID,
                    RestaurantName = res.Restaurant?.Name,
                    UserName = res.Customer?.Email,
                    Status = res.Status,
                    TotalAmount = res.TotalAmount,
                    CreatedAt = res.CreatedAt
                })
                .ToList();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = paginatedOrders
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }
    }


    //[HttpPut("UpdateOrderStatus/{orderId}/status")]
    //public async Task<IActionResult> UpdateOrderStatus(int orderId, [FromForm] Models.Models.OrderStatus newStatus)
    //{
    //    try
    //    {
    //        await _orderService.UpdateOrderStatusAsync(orderId, newStatus);
    //        await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "OrderStatusUpdated", new { OrderId = orderId, Status = newStatus });
    //        return Ok($"✅ Order {orderId} status updated to {newStatus}.");
    //    }
    //    catch (Exception ex)
    //    {
    //        return StatusCode(500, $"❌ Error: {ex.Message}");
    //    }
    //}
    [HttpGet("GetAllReservations")]
    public async Task<IActionResult> GetAllReservationByRestaurant(
     int restaurantId,
     string? search = null,
     int pageNumber = 1
     )
    {
        int pageSize = 15;
        try
        {
            var restaurant = await _restaurantService.GetRestaurantByIdAsync(restaurantId);
            if (restaurant == null)
                return NotFound(new { Message = $"Restaurant with ID {restaurantId} not found." });

            var reservations = await _reservationService.GetReservationsByRestaurantAsync(restaurantId);

            // Apply search by email if provided
            if (!string.IsNullOrEmpty(search))
            {
                reservations = reservations
                    .Where(r => r.Customer != null && r.Customer.Email.ToLower().Contains(search.ToLower()))
                    .ToList();
            }


            var totalCount = reservations.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var pagedReservations = reservations
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    ReservationID = r.ReservationID,
                    RestaurantName = r.Restaurant.Name,
                    StartTime = r.TimeSlot?.StartTime.ToString("HH:mm"),
                    EndTime = r.TimeSlot?.EndTime.ToString("HH:mm"),
                    TableId = r.TableId,
                    ReservationDate = r.ReservationDate,
                    CreatedAt = r.CreatedAt,
                    Status = r.Status,
                    CustomerEmail = r.Customer?.Email
                })
                .ToList();

            return Ok(new
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Data = pagedReservations
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while retrieving reservations.", Error = ex.Message });
        }
    }
    [HttpGet("GetRestaurantReview")]
    public async Task<IActionResult> GetRestaurantReview(int RestID, [FromQuery] int page = 1)
    {
        try
        {
            int pageSize = 20;
            var Review = await _reviewService.GetReviewsByRestaurantAsync(RestID);
            int totalCount = Review.Count();

            var paginatedOrders = Review
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(res => new
                {
                    ReviewID = res.ReviewID,
                    UserName = res.Customer?.Email,
                    Rating = res.Rating,
                    Comment=res.Comment,
                    CreatedAt = res.CreatedAt
                })
                .ToList();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = paginatedOrders
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"❌ Error: {ex.Message}");
        }

    }
}

