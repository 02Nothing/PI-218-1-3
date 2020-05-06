﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.Configuration;
using BLL.Models;
using BLL.UserModels;
using DAL.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace UIL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _repo;
        private readonly IConfigurationSection _config; //объект IConfiguration по сути хранит все конфигурационные настройки в виде набора пар "ключ"-"значение".
        private readonly IMapper _mapper;// !!! позволяет проецировать одну модель на другую, что позволяет сократить объемы кода и упростить программу.
        public AuthController(IAuthRepository repo, IConfigurationSection config, IMapper mapper)
        {
            _mapper = mapper;
            _config = config;
            _repo = repo;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserForRegister userForRegister)
        {
            userForRegister.Username = userForRegister.Username.ToLower();

            if (await _repo.UserExists(userForRegister.Username))
                return BadRequest("Username already exists");

            var userToCreate = _mapper.Map<User>(userForRegister);

            var createdUser = await _repo.Register(userToCreate, userForRegister.Password);

            return CreatedAtRoute("GetUser", new
            {
                controller = "Users",
                id = createdUser.Id
            }, createdUser);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserForLogin userForLoginDto)
        {
            var userFromRepo = await _repo.Login(userForLoginDto.Username
                .ToLower(), userForLoginDto.Password);

            if (userFromRepo == null)
                return Unauthorized();

            var claims = new[] //Объекты Claim позволяют нам сохранять дополнительную информацию, связанную с текущим пользователем.
            {
                new Claim(ClaimTypes.NameIdentifier, userFromRepo.Id.ToString()),
                new Claim(ClaimTypes.Name, userFromRepo.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.GetSection("AppSettings:Token").Value));//appsetings

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);//криптографический ключ и алгоритмы безопасности

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            var user = _mapper.Map<UserForList>(userFromRepo);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                user
            });
        }
    }
}