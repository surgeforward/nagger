﻿namespace Nagger.Services
{
    using System.Collections.Generic;
    using Interfaces;
    using Models;

    public class ProjectService : IProjectService
    {
        readonly ILocalProjectRepository _localProjectRepository;
        readonly IRemoteProjectRepository _remoteProjectRepository;

        public ProjectService(ILocalProjectRepository localProjectRepository,
            IRemoteProjectRepository remoteProjectRepository)
        {
            _localProjectRepository = localProjectRepository;
            _remoteProjectRepository = remoteProjectRepository;
        }

        public IEnumerable<Project> GetProjects()
        {
            SyncProjectsWithRemote();
            return _localProjectRepository.GetProjects();
        }

        void SyncProjectsWithRemote()
        {
            /**
             * Todo: look into a way to improve the way projects are retrieved. Ideally we would only pull the ones we don't have
            **/
            var remoteProjects = _remoteProjectRepository.GetProjects();

            foreach (var remoteProject in remoteProjects)
            {
                _localProjectRepository.StoreProject(remoteProject);
            }
        }
    }
}