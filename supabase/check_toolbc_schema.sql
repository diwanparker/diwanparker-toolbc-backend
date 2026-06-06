select table_name
from information_schema.tables
where table_schema = 'public'
  and table_name in (
    'Users',
    'DoctorProfiles',
    'PatientProfiles',
    'TreatmentPlans',
    'MedicationDoseLogs',
    'SymptomLogs',
    'Reminders',
    'Notifications',
    '__EFMigrationsHistory'
  )
order by table_name;
