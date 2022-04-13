#!/bin/bash

if [ $# -eq 0 ]
then
  exit 0
fi

while [ 1 ]
do
  dotnet run --project ImapCrawler/ImapCrawler.csproj -- $1

  if [ ! -f 'invalid_unique_id.json' ]
  then
    break
  fi
done
