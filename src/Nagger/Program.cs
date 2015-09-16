﻿namespace Nagger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Data;
    using Data.JIRA;
    using Interfaces;
    using Models;
    using Quartz;
    using Quartz.Impl;
    using Services;
    using Autofac;

    internal static class ConsoleUtil
    {
        const int Hide = 0;
        const int Show = 5;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void HideWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, Hide);
        }

        public static void ShowWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, Show);
        }
    }

    internal class Program
    {
        // note: Elysium can be used for WPF theming - seems like pretty easily
        //http://bizvise.com/2012/09/27/how-to-create-metro-style-window-on-wpf-using-elysium/


        // jira documentation for api:
        //https://docs.atlassian.com/jira/REST/latest/#id165531

        // jira api browser: https://bunjil.jira-dev.com/plugins/servlet/restbrowser#/resource/api-2-issue-issueidorkey-worklog

        //get all projects: https://www.example.com/rest/api/latest/project
        // - search for issues https://www.example.com/rest/api/latest/search
        // get all ProjectName issues
        // https://www.example.com/rest/api/latest/search?jql=project%3D%22ProjectName%22

        // schedule a task to run
        // use hangfire or Quartz.net to schedule tasks: http://hangfire.io/

        static IContainer Container { get; set; }

        static bool _running = false;
        static int _runMiss = 0;

        static void RegisterComponents(ContainerBuilder builder)
        {
            builder.RegisterType<JiraRemoteProjectRepository>().As<IRemoteProjectRepository>();
            builder.RegisterType<JiraRemoteTaskRepository>().As<IRemoteTaskRepository>();
            builder.RegisterType<JiraRemoteTimeRepository>().As<IRemoteTimeRepository>();

            builder.RegisterType<LocalProjectRepository>().As<ILocalProjectRepository>();
            builder.RegisterType<LocalTaskRepository>().As<ILocalTaskRepository>();
            builder.RegisterType<LocalTimeRepository>().As<ILocalTimeRepository>();

            builder.RegisterType<SettingsRepository>().As<ISettingsRepository>();

            builder.RegisterType<ProjectService>().As<IProjectService>();
            builder.RegisterType<SettingsService>().As<ISettingsService>();
            builder.RegisterType<TaskService>().As<ITaskService>();
            builder.RegisterType<TimeService>().As<ITimeService>();
            builder.RegisterType<ConsoleInputService>().As<IInputService>();

            builder.RegisterType<BaseJiraRepository>();
        }

        static void SetupIocContainer()
        {
            var builder = new ContainerBuilder();
            RegisterComponents(builder);
            Container = builder.Build();
        }

        static void Schedule()
        {
            var scheduler = StdSchedulerFactory.GetDefaultScheduler();
            scheduler.Start();

            var job = JobBuilder.Create<JobRunner>()
                .WithIdentity("naggerJob")
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity("naggerJob")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(15)
                    .RepeatForever())
                .Build();

            scheduler.ScheduleJob(job, trigger);
        }

        static Task AskForTask(IInputService inputService, ITaskService taskService)
        {
            var idIsKnown = inputService.AskForBoolean("Do you know the key of the task you are working on? (example: CAT-102)");
            if (!idIsKnown) return null;
            var taskId = inputService.AskForInput("What is the task key?");
            return taskService.GetTaskByName(taskId);
        }

        static Task AskForTask(IProjectService projectService, IInputService inputService, ITaskService taskService)
        {
            var task = AskForTask(inputService, taskService);
            if (task != null) return task;

            Console.WriteLine("Ok. Let's figure out what you are working on. We're going to list some projects.");
            var projects = projectService.GetProjects().ToList();
            Console.WriteLine(OutputProjects(projects));
            var projectId = inputService.AskForInput("Which project Id are you working on?");
            Console.WriteLine("Getting tasks for that project. This might take a while.");
            var tasks = taskService.GetTasksByProjectId(projectId);
            Console.WriteLine("Ok. We've got the tasks. Outputting the tasks for that project.");
            Console.WriteLine(OutputTasks(tasks));

            return AskForTask(inputService, taskService);
        }

        static void AskAboutBreak(IInputService inputService, ITimeService timeService, Task currentTask, DateTime askTime, int missedInterval)
        {
            if (!inputService.AskForBoolean("Looks like we missed " + missedInterval + " check in(s). Were you on break?"))
            {
                timeService.RecordTime(currentTask, askTime);
            }
            else
            {
                // insert an internal time marker for ask time
                timeService.RecordMarker(askTime);

                if (inputService.AskForBoolean("Have you worked on anything since you've been back?"))
                {
                    var intervalsMissed = timeService.GetIntervalMinutes(_runMiss).ToList();

                    var minutesWorked = inputService.AskForSelection("Which of these options represents about how long you have been working?", intervalsMissed);

                    timeService.RecordTime(currentTask, _runMiss, minutesWorked, askTime);
                }
                else
                {
                    // if no: insert a time entry for the current task for right now
                    timeService.RecordTime(currentTask, DateTime.Now);
                }
            }
        }

        static void Run()
        {
            using (var scope = Container.BeginLifetimeScope())
            {       
                var projectService = scope.Resolve<IProjectService>();
                var taskService = scope.Resolve<ITaskService>();
                var inputService = scope.Resolve<IInputService>();
                var timeService = scope.Resolve<ITimeService>();

                if (projectService == null || taskService == null) return;

                var askTime = DateTime.Now;

                timeService.DailyTimeSync();

                ConsoleUtil.ShowWindow();
                var currentTask = taskService.GetLastTask();
                if (currentTask != null)
                {
                    var stillWorking = inputService.AskForBoolean("Are you still working on " + currentTask.Name+"?");
                    if (!stillWorking) currentTask = null;
                }
                if (currentTask == null) currentTask = AskForTask(projectService, inputService, taskService);

                if (currentTask == null)
                {
                    ConsoleUtil.HideWindow();
                    return;
                }

                // keep track of if we missed a check in with a variable set in the execute method (maybe a miss count)
                // check the variable here, if it's true then we missed a check in
                if (_runMiss == 0) timeService.RecordTime(currentTask, askTime);
                else
                {
                    AskAboutBreak(inputService, timeService, currentTask, askTime, _runMiss);
                }
                ConsoleUtil.HideWindow();
            }
        }

        static void Main(string[] args)
        {
            // task scheduler example:
            //https://taskscheduler.codeplex.com/documentation

            // example url
            //https://www.example.com/rest/api/latest/issue/cat-262

            SetupIocContainer();
            Schedule();
        }

        #region OutputProjects

        static string OutputProjects(ICollection<Project> projects)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Outputting {0} Projects...", projects.Count).AppendLine();
            foreach (var project in projects)
            {
                sb.AppendFormat("ID: {0} || Name: {1} || Key: {2}", project.Id, project.Name, project.Key);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        class JobRunner : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                if (_running)
                {
                    _runMiss++;
                    return;
                }
                _running = true;
                Run();
                _runMiss = 0;
                _running = false;
            }
        }

        #region OutputTasks

        static string OutputTasks(IEnumerable<Task> tasks)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Outputting Tasks...");

            foreach (var task in tasks)
            {
                sb.Append(TaskString(task));
                if (task.HasTasks) sb.Append(OutputTasks(task.Tasks));
            }

            return sb.ToString();
        }

        static string TaskString(Task task)
        {
            var beginningSpace = string.Empty;

            var checkingTask = task;
            while (checkingTask.HasParent)
            {
                beginningSpace += "  ";
                checkingTask = checkingTask.Parent;
            }

            return String.Format("{0}Name: {1} || Description: {2} || HasTasks: {3} {4}", beginningSpace, task.Name, task.Description,
                task.HasTasks, Environment.NewLine);
        }

        #endregion
    }
}