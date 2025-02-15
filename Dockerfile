FROM mcr.microsoft.com/dotnet/sdk:8.0

# Install git with non-interactive frontend
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && \
    apt-get install -y --no-install-recommends git && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY . .

# Install Incrementalist globally
RUN dotnet pack src/Incrementalist.Cmd -c Release && \
    dotnet tool install --add-source src/Incrementalist.Cmd/bin/Release incrementalist --global

# Add dotnet tools to PATH
ENV PATH="${PATH}:/root/.dotnet/tools"

# Create a test solution and initialize git
RUN mkdir /test && cd /test && \
    git init && \
    git config --global user.email "test@example.com" && \
    git config --global user.name "Test User" && \
    git config --global init.defaultBranch main && \
    dotnet new sln -n TestSolution && \
    dotnet new console -n TestProject && \
    dotnet sln add TestProject/TestProject.csproj && \
    git add . && \
    git commit -m "Initial commit" && \
    git branch dev && \
    git checkout dev

WORKDIR /test
CMD ["/bin/bash"]
