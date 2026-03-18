Set-Location -Path "d:\personalproject\ticket-pulse"
New-Item -ItemType Directory -Force -Path "src"
Set-Location -Path "src"
dotnet new aspire-apphost -n AppHost --force
dotnet new aspire-servicedefaults -n ServiceDefaults --force
dotnet new web -n ApiGateway --force
dotnet new webapi -n BookingService --force
Set-Location -Path ".."
dotnet sln TicketPulse.sln add src/AppHost
dotnet sln TicketPulse.sln add src/ServiceDefaults
dotnet sln TicketPulse.sln add src/ApiGateway
dotnet sln TicketPulse.sln add src/BookingService
dotnet add src/ApiGateway/ApiGateway.csproj reference src/ServiceDefaults/ServiceDefaults.csproj
dotnet add src/BookingService/BookingService.csproj reference src/ServiceDefaults/ServiceDefaults.csproj

Set-Location -Path "src"
npx @nestjs/cli new catalog-service --package-manager npm --strict --skip-git --skip-install
npx express-generator payment-service --no-view
npx create-next-app@latest frontend --ts --tailwind --eslint --app --src-dir --import-alias "@/*" --use-npm --skip-install

Set-Location -Path ".."
