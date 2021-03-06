﻿using BuildUp.API.Entities;
using BuildUp.API.Models;
using BuildUp.API.Models.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BuildUp.API.Services.Interfaces
{
    public interface IProjectsService
    {
        Task<Project> GetProjectAsync(string builderId);
        Task<Project> GetProjectFromIdAsync(string projectId);

        Task<string> SubmitProjectAsync(ProjectSubmitModel projectSubmitModel);
        Task UpdateProjectAsync(string projectId, ProjectUpdateModel projectUpdateModel);
        Task UpdateProjectBuildOnStep(string projectId, string newBuildOn, string newBuildOnStep);
    }
}
