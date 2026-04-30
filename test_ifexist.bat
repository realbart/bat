@echo off
if exist "Z:\no_such_file_ever.xyz" (echo should not print) else echo file not exists: correct
