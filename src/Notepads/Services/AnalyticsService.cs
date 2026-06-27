// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using System.Collections.Generic;

    public static class AnalyticsService
    {
        public static void TrackEvent(string eventName, IDictionary<string, string> properties = null)
        {
            // Personal app - store telemetry is disabled
        }

        public static void TrackError(Exception exception, IDictionary<string, string> properties = null)
        {
            // Personal app - store telemetry is disabled
        }
    }
}