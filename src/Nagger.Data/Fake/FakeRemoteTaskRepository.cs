﻿namespace Nagger.Data.Fake
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Interfaces;
    using Models;

    public class FakeRemoteTaskRepository : IRemoteTaskRepository
    {
        public Task GetTaskByName(string name)
        {
            return GetTasks().First();
        }

        public IEnumerable<Task> GetTasks()
        {
            var tasks = new List<Task>();

            var task1 = GetTask("FakeTask1", "A Simple task with no subtasks");
            var task2 = GetTask("FakeTask2", "Create a Fake task repo for Nagger");
            task2.Tasks = new List<Task>
            {
                GetTask("FakeTask2_SubTask1", "Add subtasks", task2),
                GetTask("FakeTask2_SubTask2", "Create a method to build tasks easily", task2),
                GetTask("FakeTask2_SubTask3", "Create the third subtask", task2)
            };

            var task3 = GetTask("FakeTask3", "Create at least three tasks in the task repo");
            task3.Tasks = new List<Task>
            {
                GetTask("FakeTask3_SubTask1", "Create only one subtask for this task", task3)
            };

            tasks.Add(task1);
            tasks.Add(task2);
            tasks.Add(task3);

            return tasks;
        }

        public IEnumerable<Task> GetTasks(Project project)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Task> GetTasksByProjectId(string projectId, string lastTaskId = "")
        {
            throw new NotImplementedException();
        }

        public void InitializeForProject(Project project)
        {
            throw new NotImplementedException();
        }

        static Task GetTask(string id, string name, Task parent = null)
        {
            return new Task
            {
                Id = id,
                Name = name,
                Parent = parent
            };
        }
    }
}
