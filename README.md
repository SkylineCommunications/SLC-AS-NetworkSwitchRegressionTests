# NetworkSwitch Regression tests

## About

Contains a set of regression tests to measure the performance of automated actions on network switches in DataMiner. These scripts can be triggered through the [Skyline QAPortal](https://catalog.dataminer.services/details/connector/8229).

## Regression scripts

### RT_NetworkSwitch_General

Evaluate how fast the information can be retrieved from the connector.

- Retrieving Interfaces
- Retrieving VLANs

### RT_NetworkSwitch_Interface

Evaluate how fast changes can be made to a specific interface. These script measure how fast it takes to add and remove the setting. So the time measusred is actually for setting the interface two times (or three times if a cleanup is needed at the start).

- AddRemoveVlan
- ChangeSettings
- GetSetAdminStates
- TryAddRemoveVlan
- TryChangeSettings

## Supported connectors

- [Aperi Chassis](https://catalog.dataminer.services/details/connector/5455)
- [Arista Manager](https://catalog.dataminer.services/details/connector/4890)
- [CISCO Nexus](https://catalog.dataminer.services/details/connector/2061)

[!TIP]
Support for other connectors can be added by creating a library for that connector that implements the INetworkSwitch interface from the [Network Devices](https://www.nuget.org/packages/Skyline.DataMiner.Utils.NetworkDevices) library.