﻿namespace Nagger.Interfaces
{
    using System.Collections.Generic;
    using Models;

    public interface IRemoteTaskRepository
    {
        IEnumerable<Task> GetTasks();
        IEnumerable<Task> GetTasks(Project project);
    }
}