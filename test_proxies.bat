@echo off
call :subr %*
exit /b

:subr
for %%A in (%*) do (
    echo %%A
)
exit /b