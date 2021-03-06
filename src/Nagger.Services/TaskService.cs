﻿namespace Nagger.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using Interfaces;
    using Models;

    public class TaskService : ITaskService
    {
        readonly ILocalTaskRepository _localTaskRepository;
        readonly IRemoteTaskRepository _remoteTaskRepository;
        readonly IAssociatedRemoteRepositoryService _associatedRemoteRepositoryService;

        public TaskService(ILocalTaskRepository localTaskRepository, IRemoteTaskRepository remoteTaskRepository, IAssociatedRemoteRepositoryService associatedRemoteRepositoryService)
        {
            _localTaskRepository = localTaskRepository;
            _remoteTaskRepository = remoteTaskRepository;
            _associatedRemoteRepositoryService = associatedRemoteRepositoryService;
        }

        public Task GetLastTask()
        {
            return _localTaskRepository.GetLastTask();
        }

        public Task GetTaskByName(string name)
        {
            var task = _localTaskRepository.GetTaskByName(name);
            if (task != null) return task;

            task = _remoteTaskRepository.GetTaskByName(name);
            if (task != null) StoreTask(task);
            return task;
        }

        public Task GetAssociatedTaskByName(string name, Project project)
        {
            var task = _localTaskRepository.GetTaskByName(name);
            if (task != null) return task;

            var remoteTaskRepository = _associatedRemoteRepositoryService.GetAssociatedRemoteTaskRepository(project);
            if (remoteTaskRepository == null) return null;

            //TODO: we need to make sure the id stored is uniqe. We could do this by specifying that this is the remoteId and then internally tracking via auto-incremeneting key.
            task = remoteTaskRepository.GetTaskByName(name);
            if (task != null) StoreTask(task);
            return task;
        }

        public Task GetTaskById(string taskId)
        {
            return _localTaskRepository.GetTaskById(taskId);
        }

        public void StoreTask(Task task)
        {
            _localTaskRepository.StoreTask(task);
        }

        public IEnumerable<Task> GetGeneralTasks()
        {
            var tasks = _localTaskRepository.GetTasks("", true).ToList();
            if (tasks.Any()) return tasks;

            tasks = _remoteTaskRepository.GetTasks().ToList();
            StoreTasks(tasks);
            return tasks;
        }

        public IEnumerable<Task> GetTasksByTaskIds(IEnumerable<string> taskIds)
        {
            return _localTaskRepository.GetTasksByTaskIds(taskIds);
        }

        public IEnumerable<Task> GetTasksByProject(Project project)
        {
            return project == null ? Enumerable.Empty<Task>() : GetTasksByProjectId(project.Id);
        }

        public IEnumerable<Task> GetTasksByProjectId(string projectId)
        {
            // we should grab the most recent task for this project
            // then when we call the remote task repository we get all tasks since the most recent one
            var mostRecentTask = GetLastSyncedTask(projectId);

            var mostRecentId = (mostRecentTask == null) ? null : mostRecentTask.Id;

            var remoteTasks = _remoteTaskRepository.GetTasksByProjectId(projectId, mostRecentId);
            StoreTasks(remoteTasks);
            return _localTaskRepository.GetTasks(projectId);
        }

        // todo: remove
        public Task GetTestTask()
        {
            return _localTaskRepository.GetTestTask();
        }

        Task GetLastSyncedTask(string projectId = null)
        {
            return _localTaskRepository.GetLastSyncedTask(projectId);
        }

        void StoreTasks(IEnumerable<Task> tasks)
        {
            foreach (var task in tasks)
            {
                _localTaskRepository.StoreTask(task);
            }
        }
    }
}