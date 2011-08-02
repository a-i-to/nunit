// ***********************************************************************
// Copyright (c) 2011 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Services;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Xml;
using NUnit.Engine.Internal;

namespace NUnit.Engine.Runners
{
	/// <summary>
	/// Summary description for ProcessRunner.
	/// </summary>
	public class ProcessRunner : AbstractTestRunner
	{
        static Logger log = InternalTrace.GetLogger(typeof(ProcessRunner));

        private ITestAgent agent;
        private ITestRunner remoteRunner;

        private RuntimeFramework runtimeFramework;

        public ProcessRunner(ServiceContext services) : base(services) { }

        #region Properties

        public RuntimeFramework RuntimeFramework
        {
            get { return runtimeFramework; }
        }

        #endregion

        #region AbstractTestRunner Overrides

        /// <summary>
        /// Explore a TestPackage and return information about
        /// the tests found.
        /// </summary>
        /// <param name="package">The TestPackage to be explored</param>
        /// <returns>A TestEngineResult.</returns>
        public override TestEngineResult Explore(TestPackage package)
        {
            this.package = package;

            this.runtimeFramework = package.GetSetting("RuntimeFramework", RuntimeFramework.CurrentFramework);

            bool enableDebug = package.GetSetting("AgentDebug", false);
            //bool enableDebug = true;

            CreateAgentAndRunner(enableDebug);

            ITestEngineResult result = this.remoteRunner.Explore(package);
            return result as TestEngineResult; // TODO: Remove need for this cast
        }

        /// <summary>
        /// Load a TestPackage for possible execution
        /// </summary>
        /// <param name="package">The TestPackage to be loaded</param>
        /// <returns>A TestEngineResult.</returns>
        public override TestEngineResult Load(TestPackage package)
		{
            log.Info("Loading " + package.Name);
			Unload();

            this.package = package;

            this.runtimeFramework = package.GetSetting("RuntimeFramework", RuntimeFramework.CurrentFramework);

            bool enableDebug = package.GetSetting("AgentDebug", false);
            //bool enableDebug = true;

            bool loaded = false;

			try
			{
                CreateAgentAndRunner(enableDebug);

                ITestEngineResult result = this.remoteRunner.Load(package);
                loaded = !result.HasErrors;
                return result as TestEngineResult; // TODO: Remove need for this cast
			}
			finally
			{
                // Clean up if the load failed
				if ( !loaded ) Unload();
			}
		}

        /// <summary>
        /// Unload any loaded TestPackage and clear
        /// the reference to the remote runner.
        /// </summary>
        public override void Unload()
        {
            if (this.remoteRunner != null)
            {
                //log.Info("Unloading " + Path.GetFileName(Test.TestName.Name));
                this.remoteRunner.Unload();
                this.remoteRunner = null;
            }
		}

        /// <summary>
        /// Run the tests in a loaded TestPackage
        /// </summary>
        /// <param name="filter">A TestFilter used to select tests</param>
        /// <returns>A TestResult giving the result of the test execution</returns>
        public override TestEngineResult Run(ITestEventHandler listener, ITestFilter filter)
        {
            return (TestEngineResult)this.remoteRunner.Run(listener, filter);
        }

        public override void Dispose()
		{
            if (this.agent != null)
            {
                log.Info("Stopping remote agent");
                agent.Stop();
                this.agent = null;
            }
        }

		#endregion

        #region Helper Methods

        private void CreateAgentAndRunner(bool enableDebug)
        {
            if (this.agent == null)
            {
                this.agent = Services.TestAgency.GetAgent(
                    runtimeFramework,
                    30000,
                    enableDebug);

                if (this.agent == null)
                    throw new Exception("Unable to acquire remote process agent");
            }

            if (this.remoteRunner == null)
                this.remoteRunner = agent.CreateRunner();
        }

        #endregion
    }
}
