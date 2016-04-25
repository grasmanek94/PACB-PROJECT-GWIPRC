@echo off
setlocal disabledelayedexpansion
set "prev="
for /f "delims=" %%F in ('sort plist.txt') do (
  set "curr=%%F"
  setlocal enabledelayedexpansion
  if !prev! neq !curr! echo !curr!
  endlocal
  set "prev=%%F"
)