# TuneinCrew
An semi-automatic radio creator for The Crew.

This tool is designed to read an XML file and using FMOD Designer 2010, take that data and convert it into a mod that be can installed in a local instance of [PitCrew](https://github.com/Telonof/PitCrew).

Requires both .NET Core 8.X and FMOD Designer 2010 to be installed, this tool has been tested on FMOD Designer v4.44.64.

## Instructions
1. Install both .NET Core 8.X and FMOD Designer 2010 to your system, then download the lastest version of TuneinCrew under Releases and unzip it.

2. Follow the guide found here to make a valid XML file for TuneinCrew.

3. Head into the terminal and call TuneinCrew with an argument towards your XML file or just drag and drop the XML onto the exe in Windows.

> If on Linux: When it asks for the location of the FDP file inside a wine drive, provide the path wine would use to get to that FDP file. For example, if it is located at /home/user/project/Radio_AAAA.fdp and in your wine prefix /home/user is mounted to D, then write D:/project/Radio_AAAA.fdp.

4. Take the .zip file generated where the XML is located and install it as a mod in PitCrew.