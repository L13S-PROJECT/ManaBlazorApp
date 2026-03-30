# CREATE PLAN

API:
POST /api/batches/planned

FLOW:
- lietotājs nospiež "izveidot plānu"
- backend izveido batch
- backend izveido tasks no toppartsteps

EXPECTED:
- tasks jāveido TIKAI no aktīviem soļiem

BUG:
- tiek izveidoti tasks no neaktīviem soļiem

CODE (SetPlanned SQL):

INSERT INTO tasks ...
JOIN producttopparts ptp ON ptp.Version_ID = bp.Version_Id
JOIN toppartsteps ts ON ts.ProductToPart_ID = ptp.ID
WHERE
  ptp.IsActive = 1
  AND ts.IsActive = 1


FIX IDEA:

- iespējams nepietiek ar IsActive
- jāskatās stage_step_type_map
- iespējams vecie step ir aktīvi, bet nav derīgi


TODO:

- pārbaudīt SetPlanned SQL
- pārliecināties ka izmanto tikai aktīvus step
- pārbaudīt vai stage_step_type_map ietekmē izvēli