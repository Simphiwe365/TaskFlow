using Microsoft.EntityFrameworkCore;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Services;
using Xunit;

namespace TaskFlow.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WhenRequestIsValid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var service = new AuthService(context);

        var result = await service.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Email = "alice@example.com",
            Password = "Password123!"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("alice", result.User.Username);
        Assert.NotEqual("Password123!", result.User.PasswordHash);
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenPasswordIsIncorrect()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var service = new AuthService(context);

        await service.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Email = "alice@example.com",
            Password = "Password123!"
        });

        var result = await service.LoginAsync(new LoginRequest
        {
            Identifier = "alice",
            Password = "WrongPassword!"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid username or password.", result.Message);
    }
}
