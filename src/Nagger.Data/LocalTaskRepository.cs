﻿namespace Nagger.Data
{
    using System.Collections.Generic;
    using Interfaces;
    using Models;

    public class LocalTaskRepository : LocalBaseRepository, ILocalTaskRepository
    {
        public IEnumerable<Task> GetTasksByProject(Project project)
        {
            return GetTasks(project.Id);
        }

        public IEnumerable<Task> GetTasks(string projectId = null)
        {
            var tasks = new List<Task>();

            using (var cnn = GetConnection())
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = @"SELECT * FROM Tasks WHERE ParentId == ''";

                if (!string.IsNullOrEmpty(projectId))
                {
                    cmd.CommandText += " AND ProjectId = @projectId";

                    cmd.Prepare();
                    cmd.Parameters.AddWithValue("@projectId", projectId);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var task = new Task
                        {
                            Id = reader.Get<string>("Id"),
                            Name = reader.Get<string>("Name"),
                            Description = reader.Get<string>("Description"),
                            Parent = GetTaskById(reader.Get<string>("ParentId"))
                            //Project = _projectRepository.GetProjectById(reader.Get<string>("ProjectId"))
                        };
                        task.Tasks = GetTaskChildren(task);

                        tasks.Add(task);
                    }
                }
            }

            return tasks;
        }

        public Task GetLastSyncedTask()
        {
            using (var cnn = GetConnection())
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = @"SELECT id FROM Tasks ORDER BY id DESC LIMIT 1";
                using (var reader = cmd.ExecuteReader())
                {
                    return !reader.Read() ? null : GetTaskById(reader.Get<string>("Id"));
                }
            }
        }

        public Task GetTaskById(string id)
        {
            using (var cnn = GetConnection())
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = @"SELECT * FROM Tasks
                                    WHERE Id = @id";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    var task = new Task
                    {
                        Id = reader.Get<string>("Id"),
                        Name = reader.Get<string>("Name"),
                        Description = reader.Get<string>("Description"),
                        Parent = GetTaskById(reader.Get<string>("ParentId"))
                        //Project = _projectRepository.GetProjectById(reader.Get<string>("ProjectId"))
                    };
                    task.Tasks = GetTaskChildren(task);

                    return task;
                }
            }
        }

        public Task GetLastTask()
        {
            using (var cnn = GetConnection())
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = @"SELECT Tasks.Id FROM Tasks
                                    INNER JOIN TimeEntries ON TimeEntries.TaskId = Tasks.Id
                                    ORDER BY TimeEntries.Id DESC LIMIT 1";
                using (var reader = cmd.ExecuteReader())
                {
                    return !reader.Read() ? null : GetTaskById(reader.Get<string>("Id"));
                }
            }
        }

        public void StoreTask(Task task)
        {
            InsertTask(task);
            StoreChildren(task);
            StoreParents(task);
        }

        // todo: remove
        public Task GetTestTask()
        {
            var task = new Task
            {
                Id = "NaggerTestTask",
                Name = "TestTaskName"
            };

            task.Parent = new Task
            {
                Id = "NaggerTestParent",
                Name = "TestParentName",
                Tasks = new List<Task> {task}
            };

            return task;
        }

        IEnumerable<Task> GetTaskChildren(Task parent)
        {
            var tasks = new List<Task>();
            using (var cnn = GetConnection())
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = @"SELECT * FROM Tasks
                                    WHERE ParentId = @id";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@id", parent.Id);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var task = new Task
                        {
                            Id = reader.Get<string>("Id"),
                            Name = reader.Get<string>("Name"),
                            Description = reader.Get<string>("Description"),
                            Parent = parent
                            //Project = _projectRepository.GetProjectById(reader.Get<string>("ProjectId"))
                        };
                        task.Tasks = GetTaskChildren(task);
                        tasks.Add(task);
                    }
                }
            }
            return tasks;
        }

        void StoreChildren(Task task)
        {
            if (!task.HasTasks) return;
            foreach (var childTask in task.Tasks)
            {
                InsertTask(childTask);
                StoreChildren(childTask);
            }
        }

        void StoreParents(Task task)
        {
            var currentTask = task;
            while (currentTask.Parent != null)
            {
                InsertTask(currentTask);
                currentTask = currentTask.Parent;
            }
        }

        void InsertTask(Task task)
        {
            using (var cnn = GetConnection())
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = @"INSERT OR IGNORE INTO Tasks (Id, Name, Description, ParentId, ProjectId)
                                VALUES (@Id, @Name, @Description, @ParentId, @ProjectId)";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@Id", task.Id);
                cmd.Parameters.AddWithValue("@Name", task.Name);
                cmd.Parameters.AddWithValue("@Description", task.Description);

                cmd.Parameters.AddWithValue("@ParentId", (task.Parent == null) ? "" : task.Parent.Id);
                cmd.Parameters.AddWithValue("@ProjectId", (task.Project == null) ? "" : task.Project.Id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
