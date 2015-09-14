﻿namespace Nagger.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ExtensionMethods;
    using Interfaces;
    using Models;

    public class TimeService : ITimeService
    {
        readonly ILocalTaskRepository _localTaskRepository;
        readonly ILocalTimeRepository _localTimeRepository;
        readonly IRemoteTimeRepository _remoteTimeRepository;
        readonly ISettingsService _settingsService;

        public TimeService(ILocalTimeRepository localTimeRepository, ILocalTaskRepository localTaskRepository,
            IRemoteTimeRepository remoteTimeRepository, ISettingsService settingsService)
        {
            _localTaskRepository = localTaskRepository;
            _localTimeRepository = localTimeRepository;
            _remoteTimeRepository = remoteTimeRepository;
            _settingsService = settingsService;
        }

        public void RecordTime(Task task)
        {
            var timeEntry = new TimeEntry(task);
            _localTimeRepository.RecordTime(timeEntry);
        }

        public void RecordTime(Task task, DateTime time)
        {
            var timeEntry = new TimeEntry(task, time);
            _localTimeRepository.RecordTime(timeEntry);
        }

        public void RecordTime(TimeEntry timeEntry)
        {
            _localTimeRepository.RecordTime(timeEntry);
        }

        public TimeEntry GetLastTimeEntry()
        {
            return _localTimeRepository.GetLastTimeEntry();
        }

        public void DailyTimeSync()
        {
            var lastSync = _settingsService.GetSetting<DateTime>("LastSyncedDate");
            if (lastSync.Date >= DateTime.Today) return;
            SyncWithRemote();
            _settingsService.SaveSetting("LastSyncedDate", DateTime.Now.ToString());
        }

        public void SyncWithRemote()
        {
            // Squash the time.
            SquashTime();

            // get the unsynced entries
            var unsyncedEntries = _localTimeRepository.GetUnsyncedEntries();

            // loop through each and record in the remote repo
            foreach (var entry in unsyncedEntries)
            {
                if (!_remoteTimeRepository.RecordTime(entry)) continue;

                entry.Synced = true;
                _localTimeRepository.UpdateSyncedOnTimeEntry(entry);
            }
        }

        public void SquashTime()
        {
            // get all time entries that have not been synced
            var unsyncedEntries = _localTimeRepository.GetUnsyncedEntries().OrderBy(x => x.TimeRecorded);
            if (!unsyncedEntries.Any()) return;

            var currentDate = DateTime.MinValue;
            TimeEntry firstEntryForTask = null;
            var squashedEntries = new List<TimeEntry>();
            var entriesToRemove = new List<TimeEntry>();
            foreach (var entry in unsyncedEntries)
            {
                if (currentDate == DateTime.MinValue) currentDate = entry.TimeRecorded.Date;

                if (currentDate != entry.TimeRecorded.Date)
                {
                    currentDate = entry.TimeRecorded.Date;
                    firstEntryForTask = null;
                }

                if (firstEntryForTask == null) firstEntryForTask = entry;

                // this makes sure that time spent between the two entries is accounted for
                firstEntryForTask = UpdateEntryWithTimeDifference(firstEntryForTask, entry);
                if (firstEntryForTask != entry && EntriesAreForSameTask(firstEntryForTask, entry))
                {
                    entriesToRemove.Add(entry);
                }
                else
                {
                    // question: what about lunches in the middle of the day? if there are no entries in the DB then how can you track lunches and breaks?
                    // do we really care about tracking lunches and breaks though... we are tracking time worked on tasks (not time on breaks).
                    squashedEntries.Add(firstEntryForTask);
                    firstEntryForTask = entry;
                }
            }

            // note: the current implementation does not update the last entry in a list of entries - that's because there is nothing to difference it against.
            // we might want to rectify that... or we just assume that they will continue to enter time
            // but what a task that is the last in the day and the only difference is to difference with the task a day later?
            // that sounds like something that needs to be fixed...

            // other thoughts: say we enter the MinutesSpent whenever we enter a task.... the minutes spent would be the difference between the current
            // recorded time and the most recently recorded time.

            _localTimeRepository.UpdateMinutesSpentOnTimeEntries(squashedEntries);
            _localTimeRepository.RemoveTimeEntries(entriesToRemove);
        }

        static bool EntriesAreForSameTask(TimeEntry firstEntry, TimeEntry secondEntry)
        {
            // first check if the entries have a task
            // if they do check if the task ids are the same
            // entries without tasks are considered different even if the comment is the same (for now)
            if (!(firstEntry.HasTask && secondEntry.HasTask)) return false;
            return firstEntry.Task.Id == secondEntry.Task.Id;
        }

        TimeSpan ApplyCeiling(TimeSpan span)
        {
            var interval = _settingsService.GetSetting<int>("NaggingInterval");
            var intervalSpan = TimeSpan.FromMinutes(interval);
            return span.Ceiling(intervalSpan);
        }

        TimeEntry UpdateEntryWithTimeDifference(TimeEntry first, TimeEntry second)
        {
            // quick notes here - the problem is that sometimes the difference between the two entries
            // is 14 minutes and sometimes it's 16 minutes.
            // so do we want to do a ceiling or not? Or perhaps there is a better way to fix this... maybe by using the time
            // that nagger asked the question as the entry time... instead of the time that the question was answered?
            // this was updated in the Run function. The time nagger asks is used as the time entry time. (This seems to be working well)


            // get the difference between the two entries and update the first
            var timeDifference = second.TimeRecorded - first.TimeRecorded;
            timeDifference = ApplyCeiling(timeDifference);
            first.MinutesSpent = (int)timeDifference.TotalMinutes;
            return first;
        }
    }
}