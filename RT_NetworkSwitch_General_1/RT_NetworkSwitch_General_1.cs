namespace RT_NetworkSwitch_General_1
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	using QAPortalAPI.Enums;
	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ConnectorAPI.Aperi.Chassis;
	using Skyline.DataMiner.ConnectorAPI.Arista.Manager;
	using Skyline.DataMiner.ConnectorAPI.Cisco.Nexus;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Utils.Devices.Network;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private INetworkSwitch mySwitch;
		private IDms thisDms;
		private IEngine engine;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			this.engine = engine;
			thisDms = engine.GetDms();
			string elementName = engine.GetScriptParam("Switch").Value;
			var element = thisDms.GetElement(elementName);
			switch (element.Protocol.Name)
			{
				case "Aperi Chassis":
					mySwitch = element.ToAperiChassis();
					break;
				case "CISCO Nexus":
					mySwitch = element.ToCiscoNexus();
					break;
				case "Arista Manager":
					mySwitch = element.ToAristaManager();
					break;
				default:
					throw new NotSupportedException($"The protocol {element.Protocol.Name} is not yet supported in this test script");
			}

			TestReport testReport = new TestReport(
			new TestInfo("Network Switch Validation", "Phoenix", new List<int> { 15337 }, "This test will validate the INetworkSwitch."),
			new TestSystemInfo("Unknown"));

			testReport.PerformanceTestCases.Add(GetInterfacePerformance());
			testReport.PerformanceTestCases.Add(GetVlansPerformance());
			engine.AddScriptOutput("report", testReport.ToJson());
		}

		private PerformanceTestCaseReport GetInterfacePerformance()
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				if (!mySwitch.Interfaces.Any())
				{
					return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, "No interfaces found", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
				}

				foreach (var interf in mySwitch.Interfaces)
				{
					if (string.IsNullOrWhiteSpace(interf.Key))
					{
						return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, "Some interface(s) have no key", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
					}

					if (string.IsNullOrWhiteSpace(interf.Name))
					{
						return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, $"Interface {interf.Key} has no name", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
					}

					if (interf.OperationalStatus == Skyline.DataMiner.Utils.Interfaces.OperationalStates.Unknown)
					{
						return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, $"Interface {interf.Name} has unknown OperationalStatus", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
					}

					if (interf.AdminStatus == Skyline.DataMiner.Utils.Interfaces.AdminSates.Unknown)
					{
						return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, $"Interface {interf.Name} has unknown AdminStatus", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
					}
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"GetInterfacePerformance Exception: {ex}");
				return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > 1000d)
			{
				return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Failure, "Took longer than 1s to retrieve the interfaces", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
			}

			return new PerformanceTestCaseReport($"Retrieving interfaces", Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}

		private PerformanceTestCaseReport GetVlansPerformance()
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				if (!mySwitch.Vlans.Any())
				{
					return new PerformanceTestCaseReport($"Retrieving VLANs", Result.Failure, "No VLANs found", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
				}

				foreach (var vlan in mySwitch.Vlans)
				{
					if (vlan.ID < 1)
					{
						return new PerformanceTestCaseReport($"Retrieving VLANs", Result.Failure, "Some VLAN(s) have ID < 1", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
					}

					if (string.IsNullOrWhiteSpace(vlan.Name))
					{
						return new PerformanceTestCaseReport($"Retrieving VLANs", Result.Failure, $"Interface {vlan.ID} has no name", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
					}
				}

				engine.GenerateInformation($"Found {mySwitch.Vlans.Count()} VLANs");
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"GetVlansPerformance Exception: {ex}");
				return new PerformanceTestCaseReport($"Retrieving VLANs", Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > 1000d)
			{
				return new PerformanceTestCaseReport($"Retrieving VLANs", Result.Failure, "Took longer than 1s to retrieve the VLANs", ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
			}

			return new PerformanceTestCaseReport($"Retrieving VLANs", Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}
	}
}