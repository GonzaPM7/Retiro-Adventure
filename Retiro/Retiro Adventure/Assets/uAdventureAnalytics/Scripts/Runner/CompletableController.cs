﻿using UnityEngine;
using System.Collections.Generic;
using System;

using uAdventure.Core;
using System.Linq;
using uAdventure.Runner;

namespace uAdventure.Analytics
{
    public class MilestoneController
    {
        public Completable.Milestone Milestone { get; private set; }

        public bool Reached { get; private set; }

        public MilestoneController(Completable.Milestone milestone)
        {
            Milestone = milestone;
        }

        public bool Update(IChapterTarget target)
        {
            bool hasBeenUpdated = false;

            if (!Reached)
            {
                switch (Milestone.getType())
                {
                    case Completable.Milestone.MilestoneType.SCENE:
                        var isTargetedScene = Milestone.getId() == target.getId();

                        if (isTargetedScene)
                        {
                            Reached = true;
                            hasBeenUpdated = true;
                        }
                        break;
                    case Completable.Milestone.MilestoneType.ENDING:
                        if(target is Cutscene)
                        {
                            if (((Cutscene)target).isEndScene())
                            {
                                Reached = true;
                                hasBeenUpdated = true;
                            }
                        }
                        break;
                    default:
                        break;
                }
                
            }
            
            return hasBeenUpdated;
        }

        public bool Update(Interactuable interactuable, string interaction = null, string targetId = null)
        {
            bool hasBeenUpdated = false;

            if (!Reached)
            {
                bool isTargetType = false;

                if (interactuable is CharacterMB)
                {
                    isTargetType = Milestone.getType() == Completable.Milestone.MilestoneType.CHARACTER;
                }
                else if (interactuable is ObjectMB)
                {
                    isTargetType = Milestone.getType() == Completable.Milestone.MilestoneType.ITEM;
                }

                var interactiveElement = (interactuable as MonoBehaviour).GetComponent<InteractiveElement>();
                if (interactiveElement != null)
                {
                    var isTargetedElement = Milestone.getId() == interactiveElement.Element.getId();

                    if (isTargetType && isTargetedElement)
                    {
                        Reached = true;
                        hasBeenUpdated = true;
                    }
                }
            }

            return hasBeenUpdated;
        }

        public bool Update()
        {
            bool hasBeenUpdated = false;

            if (!Reached)
            {
                switch (Milestone.getType())
                {
                    case Completable.Milestone.MilestoneType.COMPLETABLE:
                        var targetCompletable = AnalyticsExtension.Instance.GetCompletable(Milestone.getId());
                        Reached = targetCompletable.End.Reached;
                        break;
                    case Completable.Milestone.MilestoneType.CONDITION:
                        Reached = ConditionChecker.check(Milestone.getConditions());
                        break;
                }

                if (Reached)
                {
                    hasBeenUpdated = true;
                }
            }

            return hasBeenUpdated;
        }

        public void Reset()
        {
            Reached = false;
        }
    }

    public class CompletableController
    {
        private readonly MilestoneController startController, endController;
        private readonly List<MilestoneController> progressControllers;

        private DateTime startTime;
        private bool completeOnExit = false;
        private bool completed = false;

        public Completable Completable { get; private set; }
        public MilestoneController Start { get { return startController; } }
        public MilestoneController End { get { return endController; } }

        public bool Started { get { return startController.Reached; } }
        public bool Completed { get { return completed; } }

        public float Progress
        {
            get
            {
                float progress = 0f;
                if (Completed)
                {
                    progress = 1f;
                }else
                {
                    var progressType = Completable.getProgress().getType();
                    switch (progressType)
                    {
                        case Completable.Progress.ProgressType.SUM:
                            progress = progressControllers.Average(m => m.Reached ? 1f : 0f);
                            break;
                        case Completable.Progress.ProgressType.SPECIFIC:
                            progress = progressControllers
                                .Where(milestone => milestone.Reached)
                                .Max(milestone => milestone.Milestone.getProgress());
                            break;
                    }
                }

                return progress;
            }
        }

        public float Score
        {
            get
            {
                var score = CalculateScore(Completable.getScore());
                return score; //TODO: enable this? Mathf.Clamp01(score / 10f);
            }
        }

        public CompletableController(Completable completable)
        {
            this.Completable = completable;

            this.startController = new MilestoneController(completable.getStart());
            this.endController = new MilestoneController(completable.getEnd());

            this.progressControllers = completable
                .getProgress()
                .getMilestones()
                .ConvertAll(c => new MilestoneController(c));
        }

        public bool UpdateMilestones()
        {
            return UpdateMilestones(milestone => milestone.Update());
        }

        private bool UpdateMilestones(Func<MilestoneController, bool> updateFunction)
        {
            if (completed)
            {
                return false;
            }

            if (!Start.Reached)
            {
                var started = updateFunction(Start);
                if (started)
                {
                    startTime = DateTime.Now;
                    AnalyticsExtension.Instance.TrackStarted(this);
                }
            }

            completed = false;

            if (Start.Reached)
            {
                bool progressed = false;
                foreach (var milestoneController in progressControllers)
                {
                    progressed |= updateFunction(milestoneController);
                }

                if (progressed)
                {
                    AnalyticsExtension.Instance.TrackProgressed(this);
                }

                if (End != null)
                {
                    completed = updateFunction(End);
                    if (completed)
                    {
                        AnalyticsExtension.Instance.TrackProgressed(this);
                        AnalyticsExtension.Instance.TrackCompleted(this, DateTime.Now - startTime);
                    }
                }
            }

            return completed;
        }

        public bool UpdateMilestones(IChapterTarget target)
        {
            if (completed)
            {
                return false;
            }

            if (completeOnExit && target.getId() != Start.Milestone.getId())
            {
                completed = true;
                AnalyticsExtension.Instance.TrackCompleted(this, DateTime.Now - startTime);
            }
            else
            {
                var wasStarted = Started;
                completed = UpdateMilestones(milestone => milestone.Update(target));
                if (wasStarted != Started && Completable.getEnd() == null)
                {
                    completeOnExit = true;
                }
            }

            return completed;
        }

        public bool UpdateMilestones(Interactuable interactuable, string interaction = null, string targetId = null)
        {
            return UpdateMilestones(milestone => milestone.Update(interactuable, interaction, targetId));
        }

        private float CalculateScore(Completable.Score completableScore)
        {
            float score = 0;

            switch (completableScore.getMethod())
            {
                // Base case (calculated based on target)
                case Completable.Score.ScoreMethod.SINGLE:
                    var targetId = completableScore.getId();
                    switch (completableScore.getType())
                    {
                        case Completable.Score.ScoreType.VARIABLE:
                            // In case of variable type, the target id points to a variable
                            var variableValue = Game.Instance.GameState.GetVariable(targetId);
                            score = variableValue;
                            break;
                        case Completable.Score.ScoreType.COMPLETABLE:
                            // In case of completable type, the target id points to a completable
                            var referencedCompletable = AnalyticsExtension.Instance.GetCompletable(targetId);
                            score = referencedCompletable.Score;
                            break;
                    }
                    break;

                // Recursive cases (calculated based on subscores)
                case Completable.Score.ScoreMethod.AVERAGE:
                    score = completableScore.getSubScores().Average(s => CalculateScore(s));
                    break;
                case Completable.Score.ScoreMethod.SUM:
                    score = completableScore.getSubScores().Sum(s => CalculateScore(s));
                    break;
            }

            return score;
        }

        public void Reset()
        {
            if (Completable.getRepeatable()) {
                if (Start != null)
                {
                    Start.Reset();
                }

                if (End != null)
                {
                    End.Reset();
                }

                completed = false;

                foreach (var milestoneController in progressControllers)
                {
                    milestoneController.Reset();
                }
            }
        }
    }

}