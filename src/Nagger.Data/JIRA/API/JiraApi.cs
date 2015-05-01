﻿using Nagger.Models;

namespace Nagger.Data.JIRA.API
{
    /**
     * Based on the example here: https://github.com/restsharp/RestSharp/wiki/Recommended-Usage
    **/

    public class JiraApi : JiraBaseApi
    {
        private const string ApiUrl = "https://www.example.com/rest/api/latest";

        public JiraApi(User user)
            : base(user, ApiUrl)
        {
            // maybe refactor this sucker down the road?
        }
    }
}