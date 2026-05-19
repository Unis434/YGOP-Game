using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ProfilesModels;
using PlayFab.ServerModels;
using PlayFab.Samples;

namespace Ygop
{
    public static class Core
    {
        public class TaskProgress
        {
            public bool unlocked;
            public int stars;
        }
        
        public class RewardData
        {
            public int rewardType;
            public int amount;
            public string itemId;
        }

        public class LevelData
        {
            public int xpToNext;
            public RewardData[] rewards;
        }

        public class CampaignData
        {
            public int taskType;
            public string id;
            public int xpFirstTime;
            public int[] xpPerStar;
            public int[] coinsPerStar;
        }

        public class DailyData
        {
            public string time;
            public CampaignData[] levels;
        }

        public class OrgData
        {
            public string id;
            public string displayName;
            public string logoUrl;
        }

        public class OrgState
        {
            public string id;
            public string displayName;
            public string logoUrl;
            public List<OrgData> orgs = new List<OrgData>();
        }

        public class OrgDatabase
        {
            public List<OrgState> states = new List<OrgState>();
        }

        static void FindOrg(OrgDatabase orgDatabase, OrgDatabase orgData, string org)
        {
            for (int i = 0; i < orgDatabase.states.Count; i++)
            {
                for (int j = 0; j < orgDatabase.states[i].orgs.Count; j++)
                {
                    if (orgDatabase.states[i].orgs[j].id == org)
                    {
                        OrgState state = new OrgState() { id = orgDatabase.states[i].id, displayName = orgDatabase.states[i].displayName, logoUrl = orgDatabase.states[i].logoUrl };
                        state.orgs.Add(orgDatabase.states[i].orgs[j]);
                        orgData.states.Add(state);
                        return;
                    }
                }
            }
        }

        [FunctionName("GET_DATA")]
        public static async Task<dynamic> GET_DATA(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string org = (string)args["org"];

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            string orgKey = "orgDataKeys_" + org;
            string orgDatabaseKey = "orgDatabase";
            var titleDataResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest() { Keys = new List<string>() { orgKey, orgDatabaseKey } });
            var titleData = titleDataResult.Result.Data;
            
            List<string> titleDataKeys = null;
            if (titleData.ContainsKey(orgKey))
                titleDataKeys = JsonConvert.DeserializeObject<List<string>>(titleData[orgKey]);
            else
                titleDataKeys =  new List<string>() { "ads_default", "campaign_default", "lessons_default", "levels_default" };
            
            titleDataResult = await PlayFabServerAPI.GetTitleDataAsync(new GetTitleDataRequest() { Keys = titleDataKeys });
            
            string orgDataStr = "{}";
            if (titleData.ContainsKey(orgDatabaseKey))
            {
                OrgDatabase orgDatabase = JsonConvert.DeserializeObject<OrgDatabase>(titleData[orgDatabaseKey]);
                OrgDatabase orgData = new OrgDatabase();
                FindOrg(orgDatabase, orgData, org);
                orgDataStr = JsonConvert.SerializeObject(orgData);
            }
            titleDataResult.Result.Data.Add("orgData", orgDataStr);

            return new { data = titleDataResult.Result.Data};
        }

        [FunctionName("GET_ORGS")]
        public static async Task<dynamic> GET_ORGS(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            List<string> orgs = ((Newtonsoft.Json.Linq.JArray)args["orgs"]).ToObject<List<string>>();
            
            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            string orgDatabaseKey = "orgDatabase";
            var titleDataResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest() { Keys = new List<string>() { orgDatabaseKey } });
            var titleData = titleDataResult.Result.Data;
            
            string orgDataStr = "{}";
            if (titleData.ContainsKey(orgDatabaseKey))
            {
                OrgDatabase orgDatabase = JsonConvert.DeserializeObject<OrgDatabase>(titleData[orgDatabaseKey]);
                for (int i = 0; i < orgDatabase.states.Count; i++)
                {
                    bool anyOrgs = false;
                    for (int j = 0; j < orgDatabase.states[i].orgs.Count; j++)
                    {
                        if (orgs.Contains(orgDatabase.states[i].orgs[j].id))
                        {
                            anyOrgs = true;
                        }
                        else
                        {
                            orgDatabase.states[i].orgs.RemoveAt(j);
                            j--;
                        }
                    }
                    if (!anyOrgs)
                    {
                            orgDatabase.states.RemoveAt(i);
                            i--;
                    }
                }
                orgDataStr = JsonConvert.SerializeObject(orgDatabase);
            }

            return new { orgData = orgDataStr };
        }

        [FunctionName("GET_DAILY")]
        public static async Task<dynamic> GET_DAILY(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            var titleDataResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest() { Keys = new List<string>()
                { "todaysDaily" } });
            var titleData = titleDataResult.Result.Data;
            
            DailyData todaysDaily = null;
            if (titleData.ContainsKey("todaysDaily"))
                todaysDaily = JsonConvert.DeserializeObject<DailyData>(titleData["todaysDaily"]);
            else
                todaysDaily = new DailyData();
                
            var time = DateTime.UtcNow;

            var lastTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(todaysDaily.time))
                lastTime = DateTime.Parse(todaysDaily.time);
            if (string.IsNullOrEmpty(todaysDaily.time) || todaysDaily.levels == null || time.Day != lastTime.Day
                    || time.Month != lastTime.Month || time.Year != lastTime.Year)
            {
                todaysDaily.time = time.ToString();
                
                titleDataResult = await PlayFabServerAPI.GetTitleDataAsync(new GetTitleDataRequest() { Keys = new List<string>()
                    { "dailyLessons", "dailyChallenges", "dailyCounts" } });
                titleData = titleDataResult.Result.Data;
                
                var dailyCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(titleData["dailyCounts"]);
                todaysDaily.levels = new CampaignData[dailyCounts["lessonCount"] + dailyCounts["challengeCount"]];
                
                var dailyLessons = JsonConvert.DeserializeObject<List<CampaignData>>(titleData["dailyLessons"]);
                for (var i = 0; i < dailyCounts["lessonCount"]; i++)
                {
                    var randInd = Random.Shared.Next(dailyLessons.Count);
                    todaysDaily.levels[i] = dailyLessons[randInd];
                    dailyLessons.RemoveAt(randInd);
                }
                var dailyChallenges = JsonConvert.DeserializeObject<List<CampaignData>>(titleData["dailyChallenges"]);
                for (var i = 0; i < dailyCounts["challengeCount"]; i++)
                {
                    var randInd = Random.Shared.Next(dailyChallenges.Count);
                    todaysDaily.levels[dailyCounts["lessonCount"] + i] = dailyChallenges[randInd];
                    dailyChallenges.RemoveAt(randInd);
                }
                
                await PlayFabServerAPI.SetTitleInternalDataAsync(new SetTitleDataRequest() { Key = "todaysDaily", Value = JsonConvert.SerializeObject(todaysDaily) });
            }

            System.Dynamic.ExpandoObject returnObj = new System.Dynamic.ExpandoObject();
            returnObj.TryAdd("levels", todaysDaily.levels);
            returnObj.TryAdd("time", time.ToString());

            var playerStatsResult = await PlayFabServerAPI.GetPlayerStatisticsAsync(new GetPlayerStatisticsRequest() {
                PlayFabId = currentPlayerId,
                StatisticNames = new List<string>() { "DAILY_0", "DAILY_1", "DAILY_2" }
            });
            var playerStats = playerStatsResult.Result.Statistics;

            for (var i = 0; i < playerStats.Count; i++)
            {
                returnObj.TryAdd(playerStats[i].StatisticName, playerStats[i].Value);
            }

            return returnObj;
        }
        
        [FunctionName("COMPLETE_TASK")]
        public static async Task<dynamic> COMPLETE_TASK(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string task = (string)args["task"];
            string challenge = (string)args["challenge"];
            int stars = (int)args["stars"];

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            var userDataResult = await PlayFabServerAPI.GetUserDataAsync(new GetUserDataRequest() {
                PlayFabId = currentPlayerId,
                Keys = new List<string>() { "Progress", "Level", "XP", "Org" }
            });
            var userData = userDataResult.Result.Data;

            Dictionary<string, TaskProgress> Progress = null;
            if (userData.ContainsKey("Progress"))
                Progress = JsonConvert.DeserializeObject<Dictionary<string, TaskProgress>>(userData["Progress"].Value);
            else
                Progress = new Dictionary<string, TaskProgress>();

            TaskProgress taskProg = new TaskProgress() { unlocked = true, stars = 0 };

            string orgKey = "orgDataKeys_" + userData["Org"].Value;
            var titleKeysDataResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest() { Keys = new List<string>() { orgKey } });
            var titleKeysData = titleKeysDataResult.Result.Data;
            
            List<string> titleDataKeys = null;
            if (titleKeysData.ContainsKey(orgKey))
                titleDataKeys = JsonConvert.DeserializeObject<List<string>>(titleKeysData[orgKey]);
            else
                titleDataKeys =  new List<string>() { "ads_default", "campaign_default", "lessons_default", "levels_default" };
            
            CampaignData[] campaign = null;
            LevelData[] levels = null;
            CampaignData taskData = null;
            var taskIndex = 0;
            if (task.StartsWith("DAILY_"))
            {
                campaign = new CampaignData[0];
                
                var titleDataResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest() { Keys = new List<string>() { "todaysDaily" } });
                var titleData = titleDataResult.Result.Data;

                DailyData todaysDaily = JsonConvert.DeserializeObject<DailyData>(titleData["todaysDaily"]);
                
                var index = int.Parse(task.Substring(task.Length - 1));
                taskData = todaysDaily.levels[index];

                if (taskData.id != challenge)
                    return new { error = "Daily challenge has ended." };

                string levelsKey = titleDataKeys.Find(x => x.StartsWith("levels_"));
                
                titleDataResult = await PlayFabServerAPI.GetTitleDataAsync(new GetTitleDataRequest() { Keys = new List<string>() { levelsKey } });
                titleData = titleDataResult.Result.Data;

                levels = JsonConvert.DeserializeObject<LevelData[]>(titleData[levelsKey]);
                
                var playerStatsResult = await PlayFabServerAPI.GetPlayerStatisticsAsync(new GetPlayerStatisticsRequest() {
                    PlayFabId = currentPlayerId,
                    StatisticNames = new List<string>() { task }
                });
                var playerStats = playerStatsResult.Result.Statistics;

                var score = 0;
                for (var i = 0; i < playerStats.Count; i++)
                {
                    if (playerStats[i].StatisticName == task)
                    {
                        score = playerStats[i].Value;
                        break;
                    }
                }
                if (score > 0 && Progress.ContainsKey(task))
                    taskProg = Progress[task];
            }
            else
            {
                string campaignKey = titleDataKeys.Find(x => x.StartsWith("campaign_"));
                string levelsKey = titleDataKeys.Find(x => x.StartsWith("levels_"));
                
                var titleDataResult = await PlayFabServerAPI.GetTitleDataAsync(new GetTitleDataRequest() { Keys = new List<string>() { campaignKey, levelsKey } });
                var titleData = titleDataResult.Result.Data;

                campaign = JsonConvert.DeserializeObject<CampaignData[]>(titleData[campaignKey]);
                levels = JsonConvert.DeserializeObject<LevelData[]>(titleData[levelsKey]);

                taskIndex = Array.FindIndex(campaign, x => x.id == task);

                if (taskIndex == -1)
                    return new { error = "Challenge data not found." };

                taskData = campaign[taskIndex];
                
                if (Progress.ContainsKey(task))
                    taskProg = Progress[task];
            }

            var Level = 0;
            if (userData.ContainsKey("Level"))
                Level = int.Parse(userData["Level"].Value);
                
            var XP = 0;
            if (userData.ContainsKey("XP"))
                XP = int.Parse(userData["XP"].Value);

            var maxStars = taskData.taskType == 0 ? 1 : 3;
            if (stars > maxStars)
                stars = maxStars;

            var xp = 0;
            var coins = 0;
            List<string> items = new List<string>();
            if (stars > 0)
            {
                if (taskProg.stars == 0)
                    xp += taskData.xpFirstTime;
                for (var i = 0; i < stars; i++)
                {
                    xp += taskData.xpPerStar[Math.Min(i, taskData.xpPerStar.Length - 1)];
                    coins += taskData.coinsPerStar[Math.Min(i, taskData.coinsPerStar.Length - 1)];
                }

                XP += xp;
                while (Level < levels.Length && XP >= levels[Level].xpToNext)
                {
                    XP -= levels[Level].xpToNext;
                    var rewards = levels[Level].rewards;
                    for (var i = 0; i < rewards.Length; i++)
                    {
                        if (rewards[i].rewardType == 0)
                            coins += rewards[i].amount;
                        else
                            items.Add(rewards[i].itemId);
                    }
                    Level++;
                }

                if (items.Count > 0)
                {
                    await PlayFabServerAPI.GrantItemsToUserAsync(new GrantItemsToUserRequest() {
                        PlayFabId = currentPlayerId,
                        ItemIds = items
                    });
                }

                if (coins > 0)
                {
                    await PlayFabServerAPI.AddUserVirtualCurrencyAsync(new AddUserVirtualCurrencyRequest() {
                        PlayFabId = currentPlayerId,
                        VirtualCurrency = "01",
                        Amount = coins
                    });
                }

                if (stars > taskProg.stars)
                {
                    await PlayFabServerAPI.UpdatePlayerStatisticsAsync(new UpdatePlayerStatisticsRequest() {
                        PlayFabId = currentPlayerId,
                        Statistics = new List<StatisticUpdate>() { new StatisticUpdate() {
                            StatisticName = "STARS",
                            Value = stars - taskProg.stars
                        }}
                    });

                    taskProg.stars = stars;
                }
            }
            Progress[task] = taskProg;
            
            if (taskIndex + 1 < campaign.Length)
            {
                var nextTask = campaign[taskIndex + 1].id;
                if (Progress.ContainsKey(nextTask))
                    Progress[nextTask].unlocked = true;
                else
                    Progress[nextTask] = new TaskProgress() { unlocked = true, stars = 0 };
            }

            await PlayFabServerAPI.UpdateUserDataAsync(new UpdateUserDataRequest() {
                PlayFabId = currentPlayerId,
                Data = new Dictionary<string, string>() {
                    { "Progress", JsonConvert.SerializeObject(Progress) },
                    { "Level", Level.ToString() },
                    { "XP", XP.ToString() }
                }
            });

            return new { xp = xp, coins = coins, items = items };
        }
        
        [FunctionName("REGISTER_STUDENT")]
        public static async Task<dynamic> REGISTER_STUDENT(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string ParentName = (string)args["ParentName"];
            string ParentEmail = (string)args["ParentEmail"];
            string StudentEmail = (string)args["StudentEmail"];

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            Dictionary<string, string> data = new Dictionary<string, string>() {
                { "ParentName", ParentName },
                { "ParentEmail", ParentEmail }
            };
            if (!string.IsNullOrWhiteSpace(StudentEmail))
                data.Add("StudentEmail", StudentEmail);

            await PlayFabServerAPI.UpdateUserDataAsync(new UpdateUserDataRequest() {
                PlayFabId = currentPlayerId,
                Data = data
            });

            return new { success = "Student registered." };
        }
        
        [FunctionName("UPDATE_AVATAR")]
        public static async Task<dynamic> UPDATE_AVATAR(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string Avatar = (string)args["Avatar"];
            string ImageUrl = (string)args["ImageUrl"];

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            await PlayFabServerAPI.UpdateUserDataAsync(new UpdateUserDataRequest() {
                PlayFabId = currentPlayerId,
                Data = new Dictionary<string, string>() {
                    { "Avatar", Avatar }
                }
            });
            
            await PlayFabServerAPI.UpdateAvatarUrlAsync(new UpdateAvatarUrlRequest() {
                PlayFabId = currentPlayerId,
                ImageUrl = ImageUrl
            });

            return new { success = "Avatar updated." };
        }
        
        [FunctionName("CREATE_PLAYER")]
        public static async Task<dynamic> CREATE_PLAYER(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string Username = (string)args["Username"];
            string Password = (string)args["Password"];
            string State = (string)args["State"];
            string Org = (string)args["Org"];

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            return await AddPlayer(Username, Password, State, Org);
        }

        static async Task<dynamic> AddPlayer(string username, string password, string State, string Org)
        {
            var regResult = await PlayFabClientAPI.RegisterPlayFabUserAsync(new PlayFab.ClientModels.RegisterPlayFabUserRequest()
            {
                TitleId = PlayFabSettings.staticSettings.TitleId,
                RequireBothUsernameAndEmail = false,
                Username = username,
                Password = password,
                DisplayName = username
            });
            
            if (regResult.Error != null)
            {
                if (regResult.Error.Error == PlayFabErrorCode.NameNotAvailable || regResult.Error.Error == PlayFabErrorCode.UsernameNotAvailable)
                {
                    username += Random.Shared.Next(0, 10);
                    return await AddPlayer(username, password, State, Org);
                }
                else
                {
                    return new { error = "Reg Error:: " + regResult.Error.GenerateErrorReport() };
                }
            }

            var orgResult = await DoUpdateOrg(regResult.Result.PlayFabId, State, Org);

            if (orgResult.GetType().GetProperty("error") != null)
                return orgResult;

            var entityKey = regResult.Result.EntityToken.Entity;
            var polResult = await PlayFabProfilesAPI.SetProfilePolicyAsync(new PlayFab.ProfilesModels.SetEntityProfilePolicyRequest()
            {
                Entity = new PlayFab.ProfilesModels.EntityKey() { Id = entityKey.Id, Type = entityKey.Type },
                Statements = new List<EntityPermissionStatement>() { new EntityPermissionStatement() { Action = "Read", Effect = EffectType.Allow, Resource = "pfrn:data--" + entityKey.Type + "!" + entityKey.Id + "/Profile/Files/Portrait.png", Principal = "*" } }
            });

            if (polResult.Error != null)
            {
                return new { error = "Policy Error:: " + polResult.Error.GenerateErrorReport(), playerId = regResult.Result.PlayFabId };
            }

            return new { playerId = regResult.Result.PlayFabId, username = username, password = password };
        }
        
        [FunctionName("UPDATE_ORG")]
        public static async Task<dynamic> UPDATE_ORG(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            dynamic args = context.FunctionArgument;

            string State = (string)args["State"];
            string Org = (string)args["Org"];

            string currentPlayerId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            PlayFabSettings.staticSettings.TitleId = context.TitleAuthenticationContext.Id;
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            return await DoUpdateOrg(currentPlayerId, State, Org);
        }

        static async Task<dynamic> DoUpdateOrg(string playerId, string State, string Org)
        {
            var updateResult = await PlayFabServerAPI.UpdateUserDataAsync(new UpdateUserDataRequest() {
                PlayFabId = playerId,
                Data = new Dictionary<string, string>() {
                    { "State", State },
                    { "Org", Org }
                }
            });

            if (updateResult.Error != null)
            {
                return new { error = "Org Update Error:: " + updateResult.Error.GenerateErrorReport(), playerId = playerId };
            }

            var userTagsResult = await PlayFabServerAPI.GetPlayerTagsAsync(new GetPlayerTagsRequest() { PlayFabId = playerId });
            
            if (userTagsResult.Error != null)
            {
                return new { error = "Org Get Tags Error:: " + userTagsResult.Error.GenerateErrorReport(), playerId = playerId };
            }

            var userTags = userTagsResult.Result.Tags;

            bool hasState = false;
            bool hasOrg = false;
            for (var i = 0; i < userTags.Count; i++)
            {
                var start = userTags[i].IndexOf('.');
                start = userTags[i].IndexOf('.', start + 1);
                var tag = userTags[i].Substring(start + 1);
                if ((tag.StartsWith("State.") && tag != "State." + State) || (tag.StartsWith("Org.") && tag != "Org." + Org))
                {
                    var removeResult = await PlayFabServerAPI.RemovePlayerTagAsync(new RemovePlayerTagRequest() {
                        PlayFabId = playerId,
                        TagName = tag
                    });

                    if (removeResult.Error != null)
                    {
                        return new { error = "Org Remove Tag Error:: " + removeResult.Error.GenerateErrorReport(), playerId = playerId };
                    }
                }
                else if (tag.StartsWith("State."))
                {
                    hasState = true;
                }
                else if (tag.StartsWith("Org."))
                {
                    hasOrg = true;
                }
            }

            if (!hasState)
            {
                var addResult = await PlayFabServerAPI.AddPlayerTagAsync(new AddPlayerTagRequest() {
                    PlayFabId = playerId,
                    TagName = "State." + State
                });

                if (addResult.Error != null)
                {
                    return new { error = "Org Add Tag Error:: " + addResult.Error.GenerateErrorReport(), playerId = playerId };
                }
            }
            
            if (!hasOrg)
            {
                var addResult = await PlayFabServerAPI.AddPlayerTagAsync(new AddPlayerTagRequest() {
                    PlayFabId = playerId,
                    TagName = "Org." + Org
                });

                if (addResult.Error != null)
                {
                    return new { error = "Org Add Tag Error:: " + addResult.Error.GenerateErrorReport(), playerId = playerId };
                }
            }
            
            return new { success = "Org updated." };
        }
    }
}
