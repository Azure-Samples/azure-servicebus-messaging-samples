//---------------------------------------------------------------------------------
// Copyright (c) 2013, Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.ServiceBus.Samples.MessagePump
{
    class PerfMeasure
    {
        Int32 count;
        Int32 stopCount;
        bool running;
        Stopwatch stopwatch;

        public PerfMeasure(Int32 stopCount)
        {
            this.stopwatch = new Stopwatch();
            this.count = 0;
            this.stopCount = stopCount;
            this.running = false;
        }

        public void StartCount()
        {
            this.running = true;
        }

        public void IncrementCount()
        {
            if (!running)
            {
                return;
            }

            Int32 newCount = Interlocked.Increment(ref this.count);
            
            if (newCount == 1)
            {
                this.stopwatch.Start();
            }

            if (newCount == this.stopCount)
            {
                this.stopwatch.Stop();
                Console.WriteLine("Elapsed time for pumping {0} messages: {1} seconds", this.stopCount, this.stopwatch.Elapsed);
            }
        }
    }
}
