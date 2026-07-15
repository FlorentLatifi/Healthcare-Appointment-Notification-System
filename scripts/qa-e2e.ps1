# E2E QA suite against live stack at http://localhost:5173
$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5173'
$api = "$base/api/v1"
$cookieJar = Join-Path $env:TEMP 'hc-qa-c1.txt'
$cookieJar2 = Join-Path $env:TEMP 'hc-qa-c2.txt'
$cookieJar3 = Join-Path $env:TEMP 'hc-qa-c3.txt'
$cookieJar4 = Join-Path $env:TEMP 'hc-qa-c4.txt'
Remove-Item $cookieJar, $cookieJar2, $cookieJar3, $cookieJar4 -ErrorAction SilentlyContinue

$script:results = New-Object System.Collections.Generic.List[object]

function Add-QaResult {
  param($Area, $Title, $Severity, $Expected, $Actual, $Pass, $Steps)
  $a = if ($null -eq $Actual) { '' } else { [string]$Actual }
  if ($a.Length -gt 450) { $a = $a.Substring(0, 450) }
  $script:results.Add([pscustomobject]@{
    Area = $Area; Title = $Title; Severity = $Severity; Pass = [bool]$Pass
    Expected = $Expected; Actual = $a; Steps = $Steps
  })
}

function Invoke-Api {
  param([string]$Method, [string]$Url, [string]$Body = $null, [string]$Token = $null, [string]$CookieFile = $null)
  $tmp = $null
  $argList = @('-sS', '-w', "`n__HTTP__%{http_code}", '-X', $Method, $Url,
    '-H', 'Content-Type: application/json', '-H', 'Accept: application/json')
  if ($Token) { $argList += @('-H', "Authorization: Bearer $Token") }
  if ($CookieFile) { $argList += @('-b', $CookieFile, '-c', $CookieFile) }
  if ($Body) {
    # Write body to file so curl does not split on spaces (PowerShell arg mangling).
    $tmp = Join-Path $env:TEMP ("hc-qa-body-{0}.json" -f [guid]::NewGuid().ToString('N'))
    [System.IO.File]::WriteAllText($tmp, $Body, [System.Text.UTF8Encoding]::new($false))
    $argList += @('--data-binary', "@$tmp")
  }
  try {
    $out = & curl.exe @argList 2>&1 | Out-String
  } finally {
    if ($tmp -and (Test-Path $tmp)) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
  }
  if ($out -match '(?s)(.*)\n__HTTP__(\d+)\s*$') {
    return @{ Body = $Matches[1].Trim(); Status = [int]$Matches[2] }
  }
  return @{ Body = $out; Status = 0 }
}

function ConvertFrom-ApiJson([string]$body) {
  try { return $body | ConvertFrom-Json } catch { return $null }
}

$ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

# ---------- UI shells ----------
foreach ($path in @('/login','/register','/dashboard','/doctors','/admin','/create-patient','/my-appointments','/doctor-dashboard','/403','/forgot-password','/reset-password')) {
  $c = Invoke-Api GET "$base$path"
  $ok = $c.Status -eq 200 -and $c.Body -match 'id="root"'
  Add-QaResult 'UI' "SPA shell $path" $(if ($ok) { 'Low' } else { 'High' }) '200 + #root' "HTTP $($c.Status) len=$($c.Body.Length)" $ok "GET $path"
}

# ---------- Public / unauth ----------
$c = Invoke-Api GET "$api/Doctors/accepting-patients?pageSize=5"
$j = ConvertFrom-ApiJson $c.Body
Add-QaResult 'API' 'Anon list accepting doctors' 'High' 'success' "HTTP $($c.Status) success=$($j.success) n=$($j.data.items.Count)" ($c.Status -eq 200 -and $j.success) 'GET Doctors/accepting-patients'

$c = Invoke-Api GET "$api/Patients"
Add-QaResult 'Security' 'Patients without token' 'Critical' '401' "HTTP $($c.Status)" ($c.Status -eq 401) 'GET /Patients'

$c = Invoke-Api GET "$api/Auth/me"
Add-QaResult 'Security' 'Me without token' 'High' '401' "HTTP $($c.Status)" ($c.Status -eq 401) 'GET /Auth/me'

# ---------- Validation / security auth ----------
$c = Invoke-Api POST "$api/Auth/register" (@{ username = 'qa_weak'; email = 'w@t.com'; password = 'weak'; role = 'Patient' } | ConvertTo-Json)
$j = ConvertFrom-ApiJson $c.Body
$msg = "$($j.message) $($j.title) $($j.errors | ConvertTo-Json -Compress)"
Add-QaResult 'Validation' 'Weak password rejected with useful message' 'High' '400 + password guidance' "HTTP $($c.Status) $msg" ($c.Status -ge 400 -and ($msg -match 'Password|password|12|character')) 'POST register weak'

$c = Invoke-Api POST "$api/Auth/register" (@{ username = '<script>alert(1)</script>'; email = "xss$ts@t.com"; password = 'SecurePass123!'; role = 'Patient' } | ConvertTo-Json)
Add-QaResult 'Security' 'XSS username rejected' 'High' '400' "HTTP $($c.Status) $($c.Body.Substring(0, [Math]::Min(180, $c.Body.Length)))" ($c.Status -ge 400) 'POST register XSS'

$c = Invoke-Api POST "$api/Auth/login" (@{ username = "admin' OR '1'='1"; password = "' OR '1'='1" } | ConvertTo-Json)
$leak = $c.Body -match 'SqlException|SELECT |stack'
Add-QaResult 'Security' 'SQLi login safe' 'Critical' '400/401 no SQL leak' "HTTP $($c.Status) leak=$leak" (($c.Status -in 400, 401) -and -not $leak) 'POST login SQLi'

$c = Invoke-Api POST "$api/Auth/login" (@{ username = ''; password = '' } | ConvertTo-Json)
Add-QaResult 'Validation' 'Empty login rejected' 'Medium' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'POST login empty'

$c = Invoke-Api POST "$api/Auth/login" (@{ username = 'admin'; password = 'WrongPassword!999' } | ConvertTo-Json)
$j = ConvertFrom-ApiJson $c.Body
Add-QaResult 'Auth' 'Wrong password fails' 'High' '400 generic fail' "HTTP $($c.Status) success=$($j.success) msg=$($j.message)" ($c.Status -eq 400 -and $j.success -eq $false) 'POST login wrong'

$c = Invoke-Api POST "$api/Auth/register" (@{ username = "wannabe$ts"; email = "wb$ts@t.com"; password = 'SecurePass123!'; role = 'Admin' } | ConvertTo-Json)
Add-QaResult 'Security' 'Cannot register as Admin' 'Critical' '400' "HTTP $($c.Status) $($c.Body.Substring(0, [Math]::Min(150, $c.Body.Length)))" ($c.Status -ge 400) 'register Admin'

$c = Invoke-Api GET "$api/Auth/me" $null 'not.a.jwt.token'
Add-QaResult 'Security' 'Garbage JWT rejected' 'High' '401' "HTTP $($c.Status)" ($c.Status -eq 401) 'bad jwt'

# ---------- Admin journey ----------
$c = Invoke-Api POST "$api/Auth/login" (@{ username = 'admin'; password = 'AdminLocal!2026' } | ConvertTo-Json) $null $cookieJar
$j = ConvertFrom-ApiJson $c.Body
$adminToken = $j.data.token
Add-QaResult 'Auth' 'Admin login token+role+username' 'Critical' 'role=Admin' "HTTP $($c.Status) success=$($j.success) role=$($j.data.role) user=$($j.data.username) token=$([bool]$adminToken)" ($c.Status -eq 200 -and $j.data.role -eq 'Admin' -and $adminToken) 'POST login admin'
$hasCookie = (Test-Path $cookieJar) -and ((Get-Content $cookieJar -Raw -ErrorAction SilentlyContinue) -match 'refreshToken')
Add-QaResult 'Auth' 'Login sets refreshToken cookie' 'Critical' 'cookie present' "hasCookie=$hasCookie" $hasCookie 'Set-Cookie'

if ($adminToken) {
  $c = Invoke-Api GET "$api/Auth/me" $null $adminToken $cookieJar
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Auth' 'Admin /Auth/me' 'High' 'role Admin' "HTTP $($c.Status) role=$($j.data.role)" ($c.Status -eq 200 -and $j.data.role -eq 'Admin') 'GET me'

  $c = Invoke-Api GET "$api/Doctors?pageSize=20" $null $adminToken $cookieJar
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Admin' 'List doctors' 'High' 'success' "HTTP $($c.Status) n=$($j.data.items.Count)" ($c.Status -eq 200 -and $j.success) 'GET Doctors'

  $c = Invoke-Api GET "$api/Patients?pageSize=20" $null $adminToken $cookieJar
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Admin' 'List patients' 'High' 'success' "HTTP $($c.Status) n=$($j.data.items.Count)" ($c.Status -eq 200 -and $j.success) 'GET Patients'

  foreach ($ep in @('/Analytics/revenue', '/Analytics/no-show-rate', '/Analytics/appointment-volume', '/AuditLogs?pageSize=5', '/Notifications')) {
    $c = Invoke-Api GET "$api$ep" $null $adminToken $cookieJar
    $isMissingFeature = $c.Status -eq 404
    Add-QaResult 'Admin' "Feature endpoint $ep" $(if ($isMissingFeature) { 'High' } else { 'Medium' }) '200 if shipped' "HTTP $($c.Status)" ($c.Status -lt 500) "GET $ep"
    if ($isMissingFeature) {
      Add-QaResult 'Missing' "No UI/API surface for $ep" 'High' 'implemented' "HTTP 404" $false "GET $ep"
    }
  }

  $c = Invoke-Api POST "$api/Doctors" (@{
      firstName = 'QA'; lastName = 'Bad'; email = "bad$ts@t.com"; phoneNumber = '+15551234567'
      licenseNumber = "B$ts"; specialty = 'General'; consultationFeeAmount = 50
      consultationFeeCurrency = 'USD'; yearsOfExperience = 5
    } | ConvertTo-Json) $adminToken $cookieJar
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Admin' 'Invalid specialty General rejected' 'High' '400' "HTTP $($c.Status) err=$($j.errors -join ';') msg=$($j.message)" ($c.Status -ge 400) 'POST Doctors General'

  $c = Invoke-Api POST "$api/Doctors" (@{
      firstName = 'QA'; lastName = 'Good'; email = "good$ts@t.com"; phoneNumber = '+15559876543'
      licenseNumber = "G$ts"; specialty = 'Cardiology'; consultationFeeAmount = 80
      consultationFeeCurrency = 'USD'; yearsOfExperience = 10
    } | ConvertTo-Json) $adminToken $cookieJar
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Admin' 'Create doctor Cardiology' 'Critical' '201' "HTTP $($c.Status) success=$($j.success) id=$($j.data)" ($c.Status -in 200, 201 -and $j.success) 'POST Doctors Cardiology'

  $c = Invoke-Api POST "$api/Patients" (@{
      firstName = 'X'; lastName = 'Y'; email = "xy$ts@t.com"; phoneNumber = '+15550001111'
      dateOfBirth = '1990-01-01T00:00:00Z'; gender = 'Male'; street = 's'; city = 'c'; state = 's'; postalCode = '1'; country = 'US'
    } | ConvertTo-Json) $adminToken $cookieJar
  Add-QaResult 'Authz' 'Admin cannot POST Patients' 'High' '403' "HTTP $($c.Status)" ($c.Status -eq 403) 'POST Patients as Admin'

  $c = Invoke-Api POST "$api/Auth/refresh" $null $null $cookieJar
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Auth' 'Refresh with cookie' 'Critical' '200 role present' "HTTP $($c.Status) success=$($j.success) role=$($j.data.role) user=$($j.data.username)" ($c.Status -eq 200 -and $j.success -and $j.data.role) 'POST refresh'
  if ($j.data.token) { $adminToken = $j.data.token }

  $c = Invoke-Api POST "$api/Auth/logout" $null $adminToken $cookieJar
  Add-QaResult 'Auth' 'Logout' 'High' '200' "HTTP $($c.Status)" ($c.Status -eq 200) 'POST logout'
  $c = Invoke-Api POST "$api/Auth/refresh" $null $null $cookieJar
  Add-QaResult 'Auth' 'Refresh after logout fails' 'High' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'POST refresh after logout'
}

# ---------- Patient journey ----------
$uname = "qapat$ts"
$c = Invoke-Api POST "$api/Auth/register" (@{ username = $uname; email = "$uname@test.com"; password = 'SecurePass123!'; role = 'Patient' } | ConvertTo-Json)
$j = ConvertFrom-ApiJson $c.Body
Add-QaResult 'Patient' 'Register Patient' 'Critical' '201' "HTTP $($c.Status) success=$($j.success)" ($c.Status -in 200, 201 -and $j.success) 'register Patient'

$c = Invoke-Api POST "$api/Auth/login" (@{ username = $uname; password = 'SecurePass123!' } | ConvertTo-Json) $null $cookieJar2
$j = ConvertFrom-ApiJson $c.Body
$pt = $j.data.token
Add-QaResult 'Patient' 'Login Patient role + null patientId' 'Critical' 'role Patient patientId null' "role=$($j.data.role) patientId=$($j.data.patientId) user=$($j.data.username)" ($j.success -and $j.data.role -eq 'Patient' -and -not $j.data.patientId) 'login Patient'

$docs = ConvertFrom-ApiJson (Invoke-Api GET "$api/Doctors/accepting-patients?pageSize=5").Body
$docId = $docs.data.items[0].id
$slot = (Get-Date).ToUniversalTime().AddDays(5).Date.AddHours(10).ToString('yyyy-MM-ddTHH:mm:ss.000Z')
$c = Invoke-Api POST "$api/Appointments" (@{ patientId = 0; doctorId = $docId; scheduledTime = $slot; reason = 'Need checkup for annual physical'; appointmentType = 'Standard' } | ConvertTo-Json) $pt $cookieJar2
Add-QaResult 'Patient' 'Book without profile rejected' 'High' '4xx' "HTTP $($c.Status) $($c.Body.Substring(0, [Math]::Min(160, $c.Body.Length)))" ($c.Status -ge 400) 'book no profile'

$c = Invoke-Api POST "$api/Patients" (@{
    firstName = 'Jane'; lastName = 'Doe'; email = "$uname@test.com"; phoneNumber = 'notaphone'
    dateOfBirth = '1990-05-05T00:00:00Z'; gender = 'Female'; street = '1 Main'; city = 'Prishtina'; state = 'KS'; postalCode = '10000'; country = 'Kosovo'
  } | ConvertTo-Json) $pt $cookieJar2
Add-QaResult 'Validation' 'Bad phone on create patient' 'High' '400 clear' "HTTP $($c.Status) $($c.Body.Substring(0, [Math]::Min(220, $c.Body.Length)))" ($c.Status -ge 400) 'POST Patients bad phone'

$c = Invoke-Api POST "$api/Patients" (@{
    firstName = 'Jane'; lastName = 'Doe'; email = "$uname@test.com"; phoneNumber = '+38344111222'
    dateOfBirth = '1990-05-05T00:00:00Z'; gender = 'Female'; street = '1 Main St'; city = 'Prishtina'; state = 'KS'; postalCode = '10000'; country = 'Kosovo'
  } | ConvertTo-Json) $pt $cookieJar2
$j = ConvertFrom-ApiJson $c.Body
$newPatientId = $j.data
Add-QaResult 'Patient' 'Create patient profile' 'Critical' '201 id>0' "HTTP $($c.Status) success=$($j.success) id=$newPatientId err=$($j.errors -join ';')" ($c.Status -in 200, 201 -and $j.success -and $newPatientId -gt 0) 'POST Patients'

$c = Invoke-Api POST "$api/Auth/refresh" $null $null $cookieJar2
$j = ConvertFrom-ApiJson $c.Body
$pt = $j.data.token
$patientId = $j.data.patientId
Add-QaResult 'Patient' 'Refresh after profile has patientId' 'Critical' 'patientId>0' "patientId=$patientId role=$($j.data.role)" ($j.success -and $patientId -gt 0) 'refresh after profile'

$apptId = $null
if ($patientId -and $docId) {
  $c = Invoke-Api GET "$api/Patients/$patientId" $null $pt $cookieJar2
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Patient' 'Get own profile' 'High' 'success' "HTTP $($c.Status) name=$($j.data.fullName)" ($c.Status -eq 200 -and $j.success) "GET Patients/$patientId"

  $past = (Get-Date).ToUniversalTime().AddDays(-2).ToString('yyyy-MM-ddTHH:mm:ss.000Z')
  $c = Invoke-Api POST "$api/Appointments" (@{ patientId = $patientId; doctorId = $docId; scheduledTime = $past; reason = 'Past appointment attempt xx'; appointmentType = 'Standard' } | ConvertTo-Json) $pt $cookieJar2
  Add-QaResult 'Validation' 'Past appointment rejected' 'High' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'book past'

  $fut = (Get-Date).ToUniversalTime().AddDays(6).Date.AddHours(11).ToString('yyyy-MM-ddTHH:mm:ss.000Z')
  $c = Invoke-Api POST "$api/Appointments" (@{ patientId = $patientId; doctorId = $docId; scheduledTime = $fut; reason = 'short'; appointmentType = 'Standard' } | ConvertTo-Json) $pt $cookieJar2
  Add-QaResult 'Validation' 'Short reason rejected' 'Medium' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'book short reason'

  foreach ($d in 7..20) {
    $s = (Get-Date).ToUniversalTime().AddDays($d).Date.AddHours(9).ToString('yyyy-MM-ddTHH:mm:ss.000Z')
    $c = Invoke-Api POST "$api/Appointments" (@{ patientId = $patientId; doctorId = $docId; scheduledTime = $s; reason = 'Annual physical examination needed now'; appointmentType = 'Standard' } | ConvertTo-Json) $pt $cookieJar2
    $j = ConvertFrom-ApiJson $c.Body
    if ($c.Status -in 200, 201 -and $j.success) {
      $apptId = $j.data.id
      if (-not $apptId) { $apptId = $j.data }
      break
    }
  }
  Add-QaResult 'Patient' 'Book appointment' 'Critical' '201' "apptId=$apptId lastHTTP=$($c.Status) body=$($c.Body.Substring(0, [Math]::Min(200, $c.Body.Length)))" ([bool]$apptId) 'book appt'

  if ($apptId) {
    $c = Invoke-Api GET "$api/Appointments/patient/${patientId}?pageSize=20" $null $pt $cookieJar2
    $j = ConvertFrom-ApiJson $c.Body
    Add-QaResult 'Patient' 'List my appointments' 'High' 'items>0' "n=$($j.data.items.Count)" ($j.success -and $j.data.items.Count -gt 0) 'list appts'

    $c = Invoke-Api GET "$api/Appointments/patient/1?pageSize=5" $null $pt $cookieJar2
    Add-QaResult 'Security' 'IDOR other patient appointments' 'Critical' '403' "HTTP $($c.Status)" ($c.Status -eq 403) 'IDOR appts'

    $c = Invoke-Api GET "$api/Patients/1" $null $pt $cookieJar2
    Add-QaResult 'Security' 'IDOR other patient profile' 'Critical' '403' "HTTP $($c.Status)" ($c.Status -eq 403) 'IDOR profile'

    $c = Invoke-Api POST "$api/Payments/create-intent" (@{ appointmentId = $apptId } | ConvertTo-Json) $pt $cookieJar2
    Add-QaResult 'Patient' 'Create payment intent' 'High' '200 or clear 4xx' "HTTP $($c.Status) $($c.Body.Substring(0, [Math]::Min(180, $c.Body.Length)))" ($c.Status -lt 500) 'payment intent'
    if ($c.Status -ge 400) {
      Add-QaResult 'Patient' 'Payment path not fully working in env' 'High' 'successful intent' "HTTP $($c.Status)" $false 'payment'
    }

    $c = Invoke-Api PUT "$api/Appointments/$apptId/cancel" (@{ appointmentId = $apptId; cancellationReason = 'nope' } | ConvertTo-Json) $pt $cookieJar2
    Add-QaResult 'Validation' 'Cancel short reason' 'Medium' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'cancel short'

    $c = Invoke-Api PUT "$api/Appointments/$apptId/cancel" (@{ appointmentId = $apptId; cancellationReason = 'Work schedule conflict requires cancellation' } | ConvertTo-Json) $pt $cookieJar2
    $j = ConvertFrom-ApiJson $c.Body
    Add-QaResult 'Patient' 'Cancel appointment' 'Critical' '200 success' "HTTP $($c.Status) success=$($j.success) err=$($j.errors -join ';')" ($c.Status -eq 200 -and $j.success) 'cancel ok'

    $c = Invoke-Api PUT "$api/Appointments/$apptId/cancel" (@{ appointmentId = $apptId; cancellationReason = 'Work schedule conflict requires cancellation' } | ConvertTo-Json) $pt $cookieJar2
    Add-QaResult 'Patient' 'Double cancel rejected' 'Medium' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'double cancel'
  }
}

# ---------- Doctor journey ----------
$dname = "qadr$ts"
$c = Invoke-Api POST "$api/Auth/register" (@{ username = $dname; email = "$dname@test.com"; password = 'SecurePass123!'; role = 'Doctor' } | ConvertTo-Json)
$j = ConvertFrom-ApiJson $c.Body
Add-QaResult 'Doctor' 'Register Doctor' 'Critical' '201' "HTTP $($c.Status) success=$($j.success)" ($c.Status -in 200, 201 -and $j.success) 'reg doctor'

$c = Invoke-Api POST "$api/Auth/login" (@{ username = $dname; password = 'SecurePass123!' } | ConvertTo-Json) $null $cookieJar3
$j = ConvertFrom-ApiJson $c.Body
$dt = $j.data.token
Add-QaResult 'Doctor' 'Doctor login' 'Critical' 'role Doctor' "role=$($j.data.role) doctorId=$($j.data.doctorId)" ($j.success -and $j.data.role -eq 'Doctor') 'login doctor'

$c = Invoke-Api POST "$api/Doctors" (@{
    firstName = 'John'; lastName = 'Smith'; email = "$dname@test.com"; phoneNumber = '+15551112222'
    licenseNumber = "D$ts"; specialty = 'Pediatrics'; consultationFeeAmount = 60
    consultationFeeCurrency = 'USD'; yearsOfExperience = 6
  } | ConvertTo-Json) $dt $cookieJar3
$j = ConvertFrom-ApiJson $c.Body
Add-QaResult 'Doctor' 'Create doctor profile' 'Critical' '201' "HTTP $($c.Status) success=$($j.success) id=$($j.data) err=$($j.errors -join ';')" ($c.Status -in 200, 201 -and $j.success) 'create doctor profile'

$c = Invoke-Api POST "$api/Auth/refresh" $null $null $cookieJar3
$j = ConvertFrom-ApiJson $c.Body
$dt = $j.data.token
$doctorId = $j.data.doctorId
Add-QaResult 'Doctor' 'Refresh links doctorId' 'Critical' 'doctorId>0' "doctorId=$doctorId" ($doctorId -gt 0) 'refresh doctor'

if ($doctorId) {
  $c = Invoke-Api GET "$api/Appointments/doctor/${doctorId}?pageSize=50" $null $dt $cookieJar3
  $j = ConvertFrom-ApiJson $c.Body
  Add-QaResult 'Doctor' 'List doctor appointments' 'High' 'success' "HTTP $($c.Status) n=$($j.data.items.Count)" ($c.Status -eq 200 -and $j.success) 'list doctor appts'
}

# Patient books with this doctor, doctor confirm+complete
$c = Invoke-Api POST "$api/Auth/login" (@{ username = $uname; password = 'SecurePass123!' } | ConvertTo-Json) $null $cookieJar2
$j = ConvertFrom-ApiJson $c.Body
$pt = $j.data.token
$patientId = $j.data.patientId
if (-not $patientId) {
  $c = Invoke-Api POST "$api/Auth/refresh" $null $null $cookieJar2
  $j = ConvertFrom-ApiJson $c.Body
  $pt = $j.data.token
  $patientId = $j.data.patientId
}
$appt2 = $null
if ($patientId -and $doctorId) {
  foreach ($d in 10..25) {
    $s = (Get-Date).ToUniversalTime().AddDays($d).Date.AddHours(14).ToString('yyyy-MM-ddTHH:mm:ss.000Z')
    $c = Invoke-Api POST "$api/Appointments" (@{ patientId = $patientId; doctorId = $doctorId; scheduledTime = $s; reason = 'Child wellness visit for checkup'; appointmentType = 'Standard' } | ConvertTo-Json) $pt $cookieJar2
    $j = ConvertFrom-ApiJson $c.Body
    if ($c.Status -in 200, 201 -and $j.success) {
      $appt2 = $j.data.id
      if (-not $appt2) { $appt2 = $j.data }
      break
    }
  }
  Add-QaResult 'E2E' 'Patient books with new doctor' 'Critical' 'appt created' "appt2=$appt2 last=$($c.Status) $($c.Body.Substring(0, [Math]::Min(150, $c.Body.Length)))" ([bool]$appt2) 'book with doctor'

  if ($appt2) {
    $c = Invoke-Api PUT "$api/Appointments/$appt2/confirm" (@{ appointmentId = $appt2; overridePaymentRequirement = $true; overrideReason = 'QA testing override for confirmation path' } | ConvertTo-Json) $dt $cookieJar3
    $j = ConvertFrom-ApiJson $c.Body
    Add-QaResult 'Doctor' 'Confirm appointment with payment override' 'Critical' '200' "HTTP $($c.Status) success=$($j.success) err=$($j.errors -join ';') msg=$($j.message)" ($c.Status -eq 200 -and $j.success) 'confirm appt'

    $c = Invoke-Api PUT "$api/Appointments/$appt2/complete" (@{ appointmentId = $appt2; doctorNotes = 'Patient examined thoroughly. All vitals normal. Follow up in 6 months.' } | ConvertTo-Json) $dt $cookieJar3
    $j = ConvertFrom-ApiJson $c.Body
    Add-QaResult 'Doctor' 'Complete appointment' 'Critical' '200' "HTTP $($c.Status) success=$($j.success) err=$($j.errors -join ';')" ($c.Status -eq 200 -and $j.success) 'complete appt'
  }
}

# ---------- Misc security ----------
$c1 = Invoke-Api POST "$api/Auth/forgot-password" (@{ email = 'admin@localhost.dev' } | ConvertTo-Json)
$c2 = Invoke-Api POST "$api/Auth/forgot-password" (@{ email = 'nope999@nowhere.invalid' } | ConvertTo-Json)
Add-QaResult 'Security' 'Forgot password no enumeration' 'High' 'same status' "s1=$($c1.Status) s2=$($c2.Status)" ($c1.Status -eq $c2.Status) 'forgot-password'

$blocked = $false; $n = 0
for ($i = 0; $i -lt 15; $i++) {
  $c = Invoke-Api POST "$api/Auth/login" (@{ username = 'nobodyx'; password = 'x' } | ConvertTo-Json)
  $n++
  if ($c.Status -eq 429) { $blocked = $true; break }
}
Add-QaResult 'Security' 'Login rate limit' 'Medium' '429' "blocked=$blocked after=$n" $blocked 'spam login'

# Mass assignment: patient A forges patient B id
$u2 = "qapatb$ts"
Invoke-Api POST "$api/Auth/register" (@{ username = $u2; email = "$u2@test.com"; password = 'SecurePass123!'; role = 'Patient' } | ConvertTo-Json) | Out-Null
$c = Invoke-Api POST "$api/Auth/login" (@{ username = $u2; password = 'SecurePass123!' } | ConvertTo-Json) $null $cookieJar4
$j = ConvertFrom-ApiJson $c.Body
$t2 = $j.data.token
Invoke-Api POST "$api/Patients" (@{
    firstName = 'Bob'; lastName = 'Roe'; email = "$u2@test.com"; phoneNumber = '+38344999888'
    dateOfBirth = '1985-01-01T00:00:00Z'; gender = 'Male'; street = '2'; city = 'P'; state = 'K'; postalCode = '1'; country = 'XK'
  } | ConvertTo-Json) $t2 $cookieJar4 | Out-Null
$c = Invoke-Api POST "$api/Auth/refresh" $null $null $cookieJar4
$j = ConvertFrom-ApiJson $c.Body
$patientId2 = $j.data.patientId

$c = Invoke-Api POST "$api/Auth/login" (@{ username = $uname; password = 'SecurePass123!' } | ConvertTo-Json) $null $cookieJar2
$j = ConvertFrom-ApiJson $c.Body
$pt = $j.data.token
$patientId = $j.data.patientId
if ($docId -and $patientId2 -and $patientId -and ($patientId -ne $patientId2)) {
  $s = (Get-Date).ToUniversalTime().AddDays(30).Date.AddHours(10).ToString('yyyy-MM-ddTHH:mm:ss.000Z')
  $c = Invoke-Api POST "$api/Appointments" (@{ patientId = $patientId2; doctorId = $docId; scheduledTime = $s; reason = 'Forged patient id booking attempt!!'; appointmentType = 'Standard' } | ConvertTo-Json) $pt $cookieJar2
  $j = ConvertFrom-ApiJson $c.Body
  $forged = $j.success -and ($j.data.patientId -eq $patientId2)
  Add-QaResult 'Security' 'Cannot book as another patientId' 'Critical' 'reject or force own id' "HTTP $($c.Status) success=$($j.success) body=$($c.Body.Substring(0, [Math]::Min(200, $c.Body.Length)))" (-not $forged) 'forge patientId'
}

$c = Invoke-Api POST "$api/Auth/register" (@{ username = $uname; email = "$uname@test.com"; password = 'SecurePass123!'; role = 'Patient' } | ConvertTo-Json)
Add-QaResult 'Validation' 'Duplicate register rejected' 'High' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'dup register'

$long = 'A' * 500
$c = Invoke-Api POST "$api/Auth/register" (@{ username = $long; email = "long$ts@t.com"; password = 'SecurePass123!'; role = 'Patient' } | ConvertTo-Json)
Add-QaResult 'Validation' 'Very long username rejected' 'Medium' '400' "HTTP $($c.Status)" ($c.Status -ge 400) 'long username'

# Frontend index
$c = Invoke-Api GET "$base/"
Add-QaResult 'Security' 'HTML does not expose source maps' 'Low' 'no .map' "hasMap=$($c.Body -match '\.map')" (-not ($c.Body -match '\.map')) 'GET /'

# Missing profile edit/delete endpoints from UX expectation
foreach ($ep in @("/Patients/$patientId", "/Doctors/$doctorId")) {
  if ($patientId -and $ep -like '/Patients*') {
    $c = Invoke-Api PUT "$api$ep" (@{ firstName = 'Jane' } | ConvertTo-Json) $pt $cookieJar2
    Add-QaResult 'Missing' "Edit profile PUT $ep" 'High' '200 or documented 405' "HTTP $($c.Status)" ($c.Status -in 200, 204, 400, 404, 405, 415) "PUT $ep"
    if ($c.Status -eq 404 -or $c.Status -eq 405) {
      Add-QaResult 'Missing' 'Patient profile edit not supported' 'High' 'edit profile feature' "HTTP $($c.Status)" $false 'PUT Patients'
    }
  }
}

# Output
$out = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -ErrorAction SilentlyContinue) 'qa-e2e-results.json'
if (-not $out -or $out -eq 'qa-e2e-results.json') {
  $out = 'C:\Users\flore\OneDrive\Desktop\Healthcare\Healthcare-Appointment-Notification-System\qa-e2e-results.json'
}
# Fix path: script is in scripts/
$out = 'C:\Users\flore\OneDrive\Desktop\Healthcare\Healthcare-Appointment-Notification-System\qa-e2e-results.json'
$script:results | ConvertTo-Json -Depth 5 | Set-Content $out -Encoding UTF8

$pass = @($script:results | Where-Object Pass).Count
$fail = @($script:results | Where-Object { -not $_.Pass }).Count
Write-Host "TOTAL=$($script:results.Count) PASS=$pass FAIL=$fail"
Write-Host ''
Write-Host '=== FAILURES ==='
$script:results | Where-Object { -not $_.Pass } | Format-Table Severity, Area, Title, Actual -Wrap | Out-String -Width 200 | Write-Host
Write-Host '=== CRITICAL ==='
$script:results | Where-Object { $_.Severity -eq 'Critical' } | Format-Table Pass, Title, Actual -Wrap | Out-String -Width 200 | Write-Host
Write-Host '=== HIGH ==='
$script:results | Where-Object { $_.Severity -eq 'High' } | Format-Table Pass, Title, Actual -Wrap | Out-String -Width 200 | Write-Host
