echo 1
goto bar

:foo
echo 3
goto baz

:bar
echo 2
goto foo

:baz
echo 4
goto :eof

:buz
goto 5