#!/usr/bin/env just --justfile

update-deps:
    dotnet restore --force-evaluate

install-deps:
    dotnet restore
