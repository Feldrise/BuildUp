﻿using BuildUp.API.Entities;
using BuildUp.API.Entities.Form;
using BuildUp.API.Entities.Status;
using BuildUp.API.Entities.Steps;
using BuildUp.API.Models.Coachs;
using BuildUp.API.Services.Interfaces;
using BuildUp.API.Settings.Interfaces;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BuildUp.API.Services
{
    public class CoachsService : ICoachsService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Coach> _coachs;
        private readonly IMongoCollection<Builder> _builders;

        private readonly IFormsService _formsService;

        public CoachsService(IMongoSettings mongoSettings, IFormsService formsService)
        {
            var mongoClient = new MongoClient(mongoSettings.ConnectionString);
            var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);

            _users = database.GetCollection<User>("users");
            _coachs = database.GetCollection<Coach>("coachs");
            _builders = database.GetCollection<Builder>("builders");

            _formsService = formsService;
        }

        public Task<Coach> GetCoachFromAdminAsync(string userId)
        {
            return GetCoachFromUserId(userId);
        }

        public async Task<Coach> GetCoachFromCoachAsync(string currentUserId, string userId)
        {
            Coach coach = await GetCoachFromUserId(userId);

            if (coach == null)
            {
                return null;
            }

            if (coach.UserId != currentUserId) throw new ArgumentException("The current user is not the coach he want's to see info", "currentUserId");

            return coach;
        }

        public async Task<List<Coach>> GetAllCoachsAsync()
        {
            return await (await _coachs.FindAsync(databaseBuilder => true)).ToListAsync();
        }

        public async Task<User> GetUserFromAdminAsync(string coachId)
        {
            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach == null) throw new Exception("The coach doesn't exist");

            return await (await _users.FindAsync(databaseUser =>
                databaseUser.Id == coach.UserId
            )).FirstOrDefaultAsync();
        }

        public async Task<User> GetUserFromBuilderAsync(string currentUserId, string coachId)
        {
            Builder builder = await GetBuilderFromUserId(currentUserId);

            if (builder == null) throw new Exception("The builder for the current user doesn't exist");
            
            if (builder.CoachId != coachId)
            {
                throw new Exception("You can't get user for this coach");
            }

            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach == null) throw new Exception("The coach doesn't exist");

            return await (await _users.FindAsync(databaseUser =>
                databaseUser.Id == coach.UserId
            )).FirstOrDefaultAsync();
        }

        public async Task<User> GetUserFromCoachAsync(string currentUserId, string coachId)
        {
            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach == null) throw new Exception("The coach doesn't exist");

            if (coach.UserId != currentUserId)
            {
                throw new Exception("You can't get user for this coach");
            }

            return await (await _users.FindAsync(databaseUser =>
                databaseUser.Id == coach.UserId
            )).FirstOrDefaultAsync();
        }

        public async Task<List<Builder>> GetBuildersFromAdminAsync(string coachId)
        {
            return await (await _builders.FindAsync(databaseUser =>
                databaseUser.CoachId == coachId
            )).ToListAsync();
        }

        public async Task<List<Builder>> GetBuildersFromCoachAsync(string currentUserId, string coachId)
        {
            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach.UserId != currentUserId) throw new Exception("This user can't see the coach builders");

            return await (await _builders.FindAsync(databaseUser =>
                databaseUser.CoachId == coachId
            )).ToListAsync();
        }

        public async Task<List<BuildupFormQA>> GetCoachFormFromAdminAsync(string coachId)
        {
            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach == null)
            {
                return null;
            }

            return await _formsService.GetFormQAsAsync(coach.UserId);
        }

        public async Task<List<BuildupFormQA>> GetCoachFormFromCoachAsync(string currentUserId, string coachId)
        {
            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach == null)
            {
                return null;
            }

            if (coach.UserId != currentUserId) throw new ArgumentException("The current user is not the coach he want's to see form answers", "currentUserId");

            return await _formsService.GetFormQAsAsync(coach.UserId);
        }

        public async Task<string> RegisterCoachAsync(CoachRegisterModel coachRegisterModel)
        {
            if (!UserExist(coachRegisterModel.UserId)) throw new ArgumentException("The user doesn't existe", "coachRegisterModel.UserId");
            if (CoachExist(coachRegisterModel.UserId)) throw new Exception("The coach already exists");

            string coachId = await RegisterToDatabase(coachRegisterModel);

            await _formsService.RegisterFormToDatabseAsync(coachRegisterModel.UserId, coachRegisterModel.FormQAs);

            return coachId;

        }

        public async Task UpdateCoachFromAdminAsync(string coachId, CoachUpdateModel coachUpdateModel)
        {
            await UpdateCoach(coachId, coachUpdateModel);
        }

        public async Task UpdateCoachFromCoachAsync(string currentUserId, string coachId, CoachUpdateModel coachUpdateModel)
        {
            Coach coach = await GetCoachFromCoachId(coachId);

            if (coach == null || coach.UserId != currentUserId)
            {
                throw new Exception("This coach can't be update by current user");
            }

            await UpdateCoach(coachId, coachUpdateModel);
        }

        public async Task RefuseCoachAsync(string coachId)
        {
            var update = Builders<Coach>.Update
                .Set(databaseCoach => databaseCoach.Status, CoachStatus.Deleted)
                .Set(databaseCoach => databaseCoach.Step, CoachSteps.Stopped);

            await _coachs.UpdateOneAsync(databaseCoach =>
                databaseCoach.Id == coachId,
                update
            );
        }

        public async Task<List<Coach>> GetCandidatingCoachsAsync()
        {
            var candidatingCoachs = await (await _coachs.FindAsync(databaseCoach =>
                databaseCoach.Status == CoachStatus.Candidating
            )).ToListAsync();

            return candidatingCoachs;
        }

        public async Task<List<Coach>> GetActiveCoachsAsync()
        {
            var activeCoachs = await (await _coachs.FindAsync(databaseCoach =>
                databaseCoach.Step == CoachSteps.Active
            )).ToListAsync();

            return activeCoachs;
        }

        private async Task<string> RegisterToDatabase(CoachRegisterModel coachRegisterModel)
        {
            Coach databaseCoach = new Coach()
            {
                UserId = coachRegisterModel.UserId,
                CandidatingDate = DateTime.Now,
                Status = CoachStatus.Candidating,
                Step = CoachSteps.Preselected,

                Department = coachRegisterModel.Department,
                Situation = coachRegisterModel.Situation,
                Description = coachRegisterModel.Description
            };

            await _coachs.InsertOneAsync(databaseCoach);

            return databaseCoach.Id;
        }

        private async Task UpdateCoach(string id, CoachUpdateModel coachUpdateModel)
        {
            await _coachs.ReplaceOneAsync(databaseCoach =>
                databaseCoach.Id == id,
                new Coach()
                {
                    Id = id,

                    UserId = coachUpdateModel.UserId,

                    Status = coachUpdateModel.Status,
                    Step = coachUpdateModel.Step,

                    Department = coachUpdateModel.Department,
                    Situation = coachUpdateModel.Situation,
                    Description = coachUpdateModel.Description
                }
            );
        }

        private async Task<Builder> GetBuilderFromUserId(string userId)
        {
            var builder = await _builders.FindAsync(databaseBuilder =>
                databaseBuilder.UserId == userId
            );

            return await builder.FirstOrDefaultAsync();
        }

        private async Task<Coach> GetCoachFromUserId(string userId)
        {
            var coach = await _coachs.FindAsync(databaseCoach =>
                databaseCoach.UserId == userId
            );

            return await coach.FirstOrDefaultAsync();
        }

        private async Task<Coach> GetCoachFromCoachId(string coachId)
        {
            var coach = await _coachs.FindAsync(databaseCoach =>
                databaseCoach.Id == coachId
            );

            return await coach.FirstOrDefaultAsync();
        }

        private bool CoachExist(string userId)
        {
            return _coachs.AsQueryable<Coach>().Any(coach =>
                coach.UserId == userId
            );
        }

        private bool UserExist(string userId)
        {
            return _users.AsQueryable<User>().Any(user =>
                user.Id == userId
            );
        }

    }
}
