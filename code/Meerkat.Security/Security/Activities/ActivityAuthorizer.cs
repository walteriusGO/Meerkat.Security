﻿using System.Security.Principal;
using System.Reflection;

using Common.Logging;

namespace Meerkat.Security.Activities
{
    /// <copydoc cref="IActivityAuthorizer.IsAuthorized" />
    public class ActivityAuthorizer : IActivityAuthorizer
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IActivityProvider provider;

        /// <summary>
        /// Creates a new instance of the <see cref="ActivityAuthorizer"/> class.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="authorized"></param>
        /// <param name="defaultActivity"></param>
        public ActivityAuthorizer(IActivityProvider provider, bool authorized, string defaultActivity = null)
        {
            this.provider = provider;

            DefaultAuthorization = authorized;
            DefaultActivity = defaultActivity;
        }

        /// <summary>
        /// Get or set the default authorization value if the authorizer has no opinion.
        /// </summary>
        public bool DefaultAuthorization { get; }

        /// <summary>
        /// Gets or sets the default activity to perform authorization against.
        /// </summary>
        public string DefaultActivity { get; }

        /// <copydoc cref="IActivityAuthorizer.IsAuthorized" />
        public AuthorizationReason IsAuthorized(string resource, string action, IPrincipal principal)
        {
            // Get the state for this request
            var defaultAuthorization = provider.DefaultAuthorization() ?? DefaultAuthorization;
            var defaultActivity = provider.DefaultActivity() ?? DefaultActivity;
            var activities = provider.Activities().ToDictionary();

            // Set up the reason
            var reason = new AuthorizationReason
            {
                Resource = resource,
                Action = action,
                Principal = principal,
                // We will always make a decision at this level
                NoDecision = false,
                IsAuthorized = defaultAuthorization
            };

            // Check the activities
            foreach (var activity in activities.FindActivities(resource, action, defaultActivity))
            {
                var rs = principal.IsAuthorized(activity, defaultAuthorization);
                if (rs.NoDecision == false)
                {
                    // Ok, we have a decision
                    reason.IsAuthorized = rs.IsAuthorized;
                    if (reason.Resource != rs.Resource || reason.Action != rs.Action)
                    {
                        // Decided based on some other activity, so say what that was
                        reason.PrincipalReason = rs;
                    }
                    else
                    {
                        // Preserve the reason information
                        reason.Reason = rs.Reason;
                    }

                    break;
                }
            }

            if (!reason.IsAuthorized)
            {
                Logger.InfoFormat("Failed authorization: User '{0}', Resource: '{1}', Action: '{2}'", principal == null ? "<Unknown>" : principal.Identity.Name, resource, action);
            }

            return reason;
        }
    }
}