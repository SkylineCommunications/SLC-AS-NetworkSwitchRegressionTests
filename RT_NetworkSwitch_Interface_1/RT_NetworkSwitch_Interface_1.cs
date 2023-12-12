/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

04/08/2023	1.0.0.1		MSA, Skyline	Initial version
****************************************************************************
*/

namespace RT_NetworkSwitch_Interface_1
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;

	using QAPortalAPI.Enums;
	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ConnectorAPI.Aperi.Chassis;
	using Skyline.DataMiner.ConnectorAPI.Arista.Manager;
	using Skyline.DataMiner.ConnectorAPI.Cisco.Nexus;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Utils.Devices.Network;
	using Skyline.DataMiner.Utils.Interfaces;
	using Skyline.DataMiner.Utils.Interfaces.Network;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private TimeSpan commandTimeout = TimeSpan.FromSeconds(120);
		private IEngine engine;
		private INetworkInterface myInterface;
		private INetworkSwitch mySwitch;
		private IDms thisDms;
		private int vlanToSet = 1001;
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			engine.Timeout = TimeSpan.FromHours(1);

			this.engine = engine;
			thisDms = engine.GetDms();
			string elementName = engine.GetScriptParam("Switch").Value;
			string interfaceName = engine.GetScriptParam("Interface").Value;
			var element = thisDms.GetElement(elementName);
			switch (element.Protocol.Name)
			{
				case "Aperi Chassis":
					var aperiSwitch = element.ToAperiChassis();
					////aperiSwitch.EnableDebugLog(engine.GenerateInformation);
					aperiSwitch.SLNetConnection = Engine.SLNetRaw;
					mySwitch = aperiSwitch;
					break;
				case "CISCO Nexus":
					var nexusSwitch = element.ToCiscoNexus();
					////nexusSwitch.EnableDebugLog(engine.GenerateInformation);
					mySwitch = nexusSwitch;
					break;
				case "Arista Manager":
					mySwitch = element.ToAristaManager();
					break;
				default:
					throw new NotSupportedException($"The protocol {element.Protocol.Name} is not yet supported in this test script");
			}

			myInterface = mySwitch.Interfaces.First(i => i.Name == interfaceName);
			TestReport testReport = new TestReport(
			new TestInfo("Network Interface Validation", "Phoenix", new List<int> { 15337 }, "This test the settings of the INetworkInterface."),
			new TestSystemInfo("Unknown"));

			engine.GenerateInformation("Run Test 'TryAddRemoveVlan'");
			testReport.PerformanceTestCases.Add(TryAddRemoveVlan());
			engine.Sleep(2000);
			engine.GenerateInformation("Run Test 'TryChangeSettings'");
			testReport.PerformanceTestCases.Add(TryChangeSettings());
			engine.Sleep(2000);
			engine.GenerateInformation("Run Test 'AddRemoveVlan'");
			mySwitch.DisableCaching();
			testReport.PerformanceTestCases.Add(AddRemoveVlan());
			engine.Sleep(2000);
			engine.GenerateInformation("Run Test 'GetSetAdminSates'");
			testReport.PerformanceTestCases.Add(GetSetAdminSates());
			engine.Sleep(2000);
			engine.GenerateInformation("Run Test 'ChangeSettings'");
			testReport.PerformanceTestCases.Add(ChangeSettings());
			mySwitch.EnableCaching();
			engine.AddScriptOutput("report", testReport.ToJson());
		}

		private PerformanceTestCaseReport AddRemoveVlan()
		{
			string testCase = "AddRemoveVlan";
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				// Remove to ensure clean interface
				if (myInterface.VLANs.Contains(vlanToSet))
				{
					myInterface.RemoveVlan(vlanToSet);
					if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return !myInterface.VLANs.Contains(vlanToSet); }, commandTimeout))
					{
						return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'RemoveVlan({vlanToSet})'", ResultUnit.Millisecond, -1);
					}
				}

				// Add VLAN
				myInterface.AddVlan(vlanToSet);
				if (!SpinWait.SpinUntil(
					() =>
					{
						Thread.Sleep(250);
						return myInterface.VLANs.Contains(vlanToSet);
					}, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'AddVlan({vlanToSet})'", ResultUnit.Millisecond, -1);
				}

				// Remove VLAN
				myInterface.RemoveVlan(vlanToSet);
				if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return !myInterface.VLANs.Contains(vlanToSet); }, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'RemoveVlan({vlanToSet})'", ResultUnit.Millisecond, -1);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"{testCase} Exception: {ex}");
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, -1);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > commandTimeout.TotalMilliseconds)
			{
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Took longer than {commandTimeout.TotalSeconds}s", ResultUnit.Millisecond, -1);
			}

			return new PerformanceTestCaseReport(testCase, Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}

		private PerformanceTestCaseReport ChangeSettings()
		{
			string testCase = "ChangeSettings";
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				var cleansettings = myInterface.Settings.RemoveVlan(vlanToSet).SetAdminState(false);
				var configureSettings = myInterface.Settings.AddVlan(vlanToSet).SetAdminState(true);

				// Remove to ensure clean interface
				if (myInterface.VLANs.Contains(vlanToSet) || myInterface.AdminStatus == AdminSates.Up)
				{
					cleansettings.Push();
					if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return !myInterface.VLANs.Contains(vlanToSet) && myInterface.AdminStatus != AdminSates.Up; }, commandTimeout))
					{
						return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'RemoveVlan({vlanToSet}).SetAdminState(false).Push()'", ResultUnit.Millisecond, -1);
					}
				}

				// Add Settings
				configureSettings.Push();
				if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return myInterface.VLANs.Contains(vlanToSet) && myInterface.AdminStatus == AdminSates.Up; }, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'AddVlan({vlanToSet}).SetAdminState(true).Push()'", ResultUnit.Millisecond, -1);
				}

				// Remove Settings
				cleansettings.Push();
				if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return !myInterface.VLANs.Contains(vlanToSet) && myInterface.AdminStatus != AdminSates.Up; }, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'RemoveVlan({vlanToSet}).SetAdminState(false).Push()'", ResultUnit.Millisecond, -1);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"{testCase} Exception: {ex}");
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, -1);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > commandTimeout.TotalMilliseconds)
			{
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Took longer than {commandTimeout.TotalSeconds}s", ResultUnit.Millisecond, -1);
			}

			return new PerformanceTestCaseReport(testCase, Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}

		private PerformanceTestCaseReport GetSetAdminSates()
		{
			string testCase = "GetSetAdminSates";
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				// Set Down to ensure clean interface
				if (myInterface.AdminStatus == AdminSates.Up)
				{
					myInterface.AdminStatus = AdminSates.Down;
					if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return myInterface.AdminStatus != AdminSates.Up; }, commandTimeout))
					{
						return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to set 'AdminSates.Down'", ResultUnit.Millisecond, -1);
					}
				}

				// Set IF Up
				myInterface.AdminStatus = AdminSates.Up;
				if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return myInterface.AdminStatus == AdminSates.Up; }, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to set 'AdminSates.Up'", ResultUnit.Millisecond, -1);
				}

				// Set IF Down
				myInterface.AdminStatus = AdminSates.Down;
				if (!SpinWait.SpinUntil(() => { Thread.Sleep(250); return myInterface.AdminStatus != AdminSates.Up; }, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to set 'AdminSates.Down'", ResultUnit.Millisecond, -1);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"{testCase} Exception: {ex}");
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, -1);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > commandTimeout.TotalMilliseconds)
			{
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Took longer than {commandTimeout.TotalSeconds}s", ResultUnit.Millisecond, -1);
			}

			return new PerformanceTestCaseReport(testCase, Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}

		private PerformanceTestCaseReport TryAddRemoveVlan()
		{
			string testCase = "TryAddRemoveVlan";
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				// Remove to ensure clean interface
				if (!myInterface.TryRemoveVlan(vlanToSet, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'TryRemoveVlan({vlanToSet}, {commandTimeout})'", ResultUnit.Millisecond, -1);
				}

				// Add VLAN
				if (!myInterface.TryAddVlan(vlanToSet, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'TryAddVlan({vlanToSet}, {commandTimeout})'", ResultUnit.Millisecond, -1);
				}

				// Remove VLAN
				if (!myInterface.TryRemoveVlan(vlanToSet, commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'TryRemoveVlan({vlanToSet}, {commandTimeout})'", ResultUnit.Millisecond, -1);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"{testCase} Exception: {ex}");
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, -1);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > commandTimeout.TotalMilliseconds)
			{
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Took longer than {commandTimeout.TotalSeconds}s", ResultUnit.Millisecond, -1);
			}

			return new PerformanceTestCaseReport(testCase, Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}

		private PerformanceTestCaseReport TryChangeSettings()
		{
			string testCase = "TryChangeSettings";
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				var cleansettings = myInterface.Settings.RemoveVlan(vlanToSet).SetAdminState(false);
				var configureSettings = myInterface.Settings.AddVlan(vlanToSet).SetAdminState(true);

				// Remove to ensure clean interface
				if (!cleansettings.TryPush(commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'RemoveVlan({vlanToSet}).SetAdminState(false).TryPush()'", ResultUnit.Millisecond, -1);
				}

				// Add Settings
				if (!configureSettings.TryPush(commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'Unable to 'AddVlan({vlanToSet}).SetAdminState(true).TryPush()'", ResultUnit.Millisecond, -1);
				}

				// Remove Settings
				if (!cleansettings.TryPush(commandTimeout))
				{
					return new PerformanceTestCaseReport(testCase, Result.Failure, $"Unable to 'RemoveVlan({vlanToSet}).SetAdminState(false).TryPush()'", ResultUnit.Millisecond, -1);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"{testCase} Exception: {ex}");
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Exception {ex.Message} caught, see information events", ResultUnit.Millisecond, -1);
			}

			sw.Stop();
			if (sw.Elapsed.TotalMilliseconds > commandTimeout.TotalMilliseconds)
			{
				return new PerformanceTestCaseReport(testCase, Result.Failure, $"Took longer than {commandTimeout.TotalSeconds}s", ResultUnit.Millisecond, -1);
			}

			return new PerformanceTestCaseReport(testCase, Result.Success, string.Empty, ResultUnit.Millisecond, sw.Elapsed.TotalMilliseconds);
		}
	}
}