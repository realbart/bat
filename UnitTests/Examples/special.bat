@echo off
:: Quoting
echo "hello world"
:: Caret escaping
echo hello^&world
echo hello^|world
echo hello^>world
echo hello^^caret
:: Percent in batch (doubled)
echo percent: 100%%
:: Empty variable
echo empty: %NOSUCHVAR_EVER%done
:: Parentheses in echo
echo (parentheses)
:: Exclamation without delayed expansion
echo hello!world
