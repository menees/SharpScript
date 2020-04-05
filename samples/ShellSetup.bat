@echo off

REM In the build environment the exe and script aren't in the same folder.
if not exist SharpScript\bin\debug\SharpScript.exe goto production
if not exist Scripts\ShellSetup.scs goto production
start SharpScript\bin\debug\SharpScript.exe Scripts\ShellSetup.scs %1 %2 %3 %4 %5 %6 %7 %8 %9
goto end

:production
REM In a production environment the exe and script should be in the same folder.
if not exist SharpScript.exe goto error
if not exist ShellSetup.scs goto error
start SharpScript.exe ShellSetup.scs %1 %2 %3 %4 %5 %6 %7 %8 %9
goto end

:error
echo Unable to find SharpScript.exe or ShellSetup.scs.
pause

:end