import path from 'node:path'
import { expect, test, type Page } from '@playwright/test'
import {
  emptyCarriers,
  ensureIdle,
  inspectFail,
  inspectPass,
  lotsOf,
  pancakesOf,
  registerMaterial,
  runOperation,
  storedReadingsInLimits,
  token,
  workOrder,
} from './api.ts'

// The README demo script, walked against the running Compose stack. Steps marked "screenshot" save the README
// images. Everything this run touches (work order, lots, carriers) is created or looked up by the run itself, so
// it passes again on a database that already holds earlier runs.

// The README images are rewritten only on request (UPDATE_SCREENSHOTS=1); otherwise the shots go to the ignored
// test-results folder, so a plain run leaves the working tree clean.
const SHOT_DIR = process.env.UPDATE_SCREENSHOTS === '1' ? '../../docs/screenshots' : '../test-results/screenshots'
const SHOT = (name: string) => path.join(import.meta.dirname, SHOT_DIR, name)

type DemoRole = 'Planner' | 'Operator' | 'QC' | 'Admin'

async function loginAs(page: Page, role: DemoRole) {
  const logout = page.getByRole('button', { name: 'Logout' })
  if (await logout.isVisible()) await logout.click()
  // Starting from "/" lands each role on its home page (Planner and Admin: Dashboard, Operator: Station, QC: Quality)
  // instead of the page the previous user was on.
  await page.goto('/')
  await page.getByRole('button', { name: `Log in as ${role}` }).click()
  await expect(logout).toBeVisible()
}

function nav(page: Page, label: string) {
  return page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: label })
}

/** Saves a README screenshot once the page has settled: no toasts, nothing loading, realtime connected. */
async function snap(page: Page, name: string, { alarmToastExpected = false } = {}) {
  if (!alarmToastExpected) expect(await page.locator('[data-sonner-toast][data-type="error"]').count()).toBe(0)
  await expect(page.locator('[data-sonner-toast]')).toHaveCount(0, { timeout: 15_000 })
  await expect(page.getByText('Loading...')).toHaveCount(0)
  await expect(page.getByRole('banner').getByText('Live', { exact: true })).toBeVisible()
  await page.mouse.move(0, 0)
  // Full page: the dashboard's eight tiles and the genealogy graph run past the 900 px viewport.
  await page.screenshot({ path: SHOT(name), fullPage: true })
}

/**
 * True once the station shows all three live parameters, each in the middle half of its limits. Each parameter
 * row carries its limits in its tooltip ("Limits 120 to 140 °C") and the reading in its value cell.
 */
async function parametersSettled(page: Page): Promise<boolean> {
  const rows = await page.getByRole('main').locator('[title^="Limits "]').all()
  if (rows.length !== 3) return false
  for (const row of rows) {
    const limits = /^Limits (\S+) to (\S+)/.exec((await row.getAttribute('title')) ?? '')
    const value = Number.parseFloat((await row.getByRole('definition').textContent()) ?? '')
    if (!limits || Number.isNaN(value)) return false
    const [low, high] = [Number(limits[1]), Number(limits[2])]
    if (Math.abs(value - (low + high) / 2) > (high - low) / 4) return false
  }
  return true
}

/** `datetime-local` value ("YYYY-MM-DDTHH:mm") in local time; the test and its browser share the machine's zone. */
function localInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

test('demo walkthrough', async ({ page, request }) => {
  const admin = await token(request, 'admin')
  // A rerun may start while CT01 is still DOWN from the previous run's injected fault; it recovers by itself.
  for (const code of ['MX01', 'CT01', 'CP01', 'SL01']) await ensureIdle(request, admin, code)

  // 1. Planner logs in: the dashboard shows all eight machines live. (screenshot)
  await loginAs(page, 'Planner')
  await expect(page).toHaveURL(/\/dashboard$/)
  const main = page.getByRole('main')
  await expect(main.getByRole('link', { name: /^(MX|CT|CP|SL)0[12]\b/ })).toHaveCount(8)
  // On a freshly started stack the simulator may still be connecting.
  await expect(main.getByText('No readings')).toHaveCount(0, { timeout: 60_000 })
  await snap(page, '01-dashboard.png')

  // 2. Planner creates a work order for 2 cathode pancakes and releases it. (screenshot)
  await nav(page, 'Work Orders').click()
  await page.getByRole('button', { name: 'New Work Order' }).click()
  const form = page.getByRole('dialog')
  const start = new Date()
  await form.getByLabel('Product', { exact: true }).selectOption('CATH-NCM811')
  await form.getByLabel('Target quantity').fill('2')
  await form.getByLabel('Planned start').fill(localInput(start))
  await form.getByLabel('Planned end').fill(localInput(new Date(start.getTime() + 24 * 3_600_000)))
  await form.getByLabel('Mixing').selectOption('MX01')
  await form.getByLabel('Coating').selectOption('CT01')
  await form.getByLabel('Calendering').selectOption('CP01')
  await form.getByLabel('Slitting').selectOption('SL01')
  await form.getByRole('button', { name: 'Save' }).click()
  await expect(page).toHaveURL(/\/work-orders\/[0-9a-f-]{36}$/)
  const workOrderId = page.url().split('/').at(-1)!
  const title = page.getByRole('heading', { level: 1 })
  await expect(title).toHaveText(/^WO-\d{6}-\d{3}$/)
  const woNumber = (await title.textContent())!.trim()
  await page.getByRole('button', { name: 'Release' }).click()
  await expect(main.getByText('RELEASED', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Hold' })).toBeVisible()
  await snap(page, '02-work-order.png')

  // 3. Receive fresh material lots (API): the four mixing ingredients and a roll of aluminium foil.
  const planner = await token(request, 'planner')
  const raws = [
    await registerMaterial(request, planner, 'NCM811', 300),
    await registerMaterial(request, planner, 'PVDF', 15),
    await registerMaterial(request, planner, 'SUPER-P', 15),
    await registerMaterial(request, planner, 'NMP', 170),
  ]
  const foil = await registerMaterial(request, planner, 'AL-FOIL', 1500)

  // 4. Operator mixes the slurry on MX01: scan the four RAW lots, track in, produce 480 kg / 20 kg reject, track out.
  await loginAs(page, 'Operator')
  await main.getByRole('link', { name: /^MX01\b/ }).click()
  await page.getByRole('radio', { name: new RegExp(woNumber) }).click()
  const scan = page.getByLabel('Scan lot or carrier')
  for (const lot of raws) {
    await scan.fill(lot)
    await scan.press('Enter')
  }
  await expect(page.getByRole('list', { name: 'Scanned inputs' }).getByRole('listitem')).toHaveCount(4)
  await page.getByRole('button', { name: 'Track in', exact: true }).click()
  await page.getByLabel('Good (kg)').fill('480')
  await page.getByLabel('Reject (kg)').fill('20')
  await page.getByRole('button', { name: 'Produce', exact: true }).click()
  await expect(page.getByText('Output recorded. Track out to finish this run.')).toBeVisible()
  await page.getByRole('button', { name: 'Track out', exact: true }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Confirm track out' }).click()
  await expect(page.getByText('1. Choose the work order')).toBeVisible()
  const operator = await token(request, 'operator')
  const slurries = await lotsOf(request, operator, woNumber, 'SLURRY')
  expect(slurries).toHaveLength(1)
  const slurry = slurries[0]

  // 5. QC inspects the slurry from the queue, typing Solid content with a decimal comma. (screenshot) PASS.
  await loginAs(page, 'QC')
  await expect(page).toHaveURL(/\/quality$/)
  await page.getByRole('row', { name: new RegExp(slurry) }).getByRole('link', { name: 'Inspect' }).click()
  await page.getByLabel('Viscosity').fill('6000')
  await page.getByLabel('Solid content').fill('70,5')
  await expect(page.getByTestId('inspection-result')).toHaveText('PASS')
  await snap(page, '04-inspection.png')
  await page.getByRole('button', { name: 'Submit inspection' }).click()
  await expect(page.getByText(`${slurry} PASS`)).toBeVisible()
  await expect(page).toHaveURL(/\/quality$/)

  // 6. Operator coats on CT01: scan the foil and the slurry, doff one 1000 m roll onto the first empty bobbin;
  //    the station shows the open run and the coater's live parameters. (screenshot) The run stays open until the
  //    trend has readings inside the limits, then tracks out with 1020 m of foil used.
  await ensureIdle(request, admin, 'CT01')
  await loginAs(page, 'Operator')
  await main.getByRole('link', { name: /^CT01\b/ }).click()
  await page.getByRole('radio', { name: new RegExp(woNumber) }).click()
  for (const lot of [foil, slurry]) {
    await scan.fill(lot)
    await scan.press('Enter')
  }
  const coatStart = new Date()
  await page.getByRole('button', { name: 'Track in', exact: true }).click()
  const [bobbin] = await emptyCarriers(request, operator, 'BB', 1)
  await page.getByLabel('Empty carrier').fill(bobbin)
  await page.getByLabel('Good (m)').fill('1000')
  await page.getByLabel('Reject (m)').fill('20')
  await page.getByRole('button', { name: 'Doff roll' }).click()
  await expect(page.getByText(new RegExp(`on ${bobbin}$`))).toBeVisible()
  // The coater ramps up from rest after track-in; wait until every parameter has settled near its setpoint.
  await expect(main.getByText('RUNNING', { exact: true }).first()).toBeVisible()
  await expect.poll(() => parametersSettled(page), { timeout: 90_000 }).toBe(true)
  await snap(page, '03-operator-station.png')
  // Readings are stored at most every 10 s; keep coating until the trend (step 10) has some inside the limits.
  await expect
    .poll(() => storedReadingsInLimits(request, operator, 'CT01', coatStart), { timeout: 120_000, intervals: [2_000] })
    .toBeGreaterThanOrEqual(3)
  await page.getByRole('button', { name: 'Track out', exact: true }).click()
  const trackOut = page.getByRole('dialog')
  await trackOut.getByLabel(`Consumed ${foil}`).fill('1020')
  await trackOut.getByRole('button', { name: 'Confirm track out' }).click()
  await expect(page.getByText('1. Choose the work order')).toBeVisible()

  // 7. The rest of the line through the API: QC passes the roll, Operator calenders it on CP01 (990 m onto an empty
  //    bobbin), QC passes it, Operator slits it on SL01 into 2 pancakes of 120 m on empty cores (lanes 3 to 8 are
  //    not needed: reject only, as the lane grid requires a quantity on every lane). QC passes pancake -01 and
  //    fails -02 (burr above the limit), which puts it on HOLD.
  const qc = await token(request, 'qc')
  const wo = await workOrder(request, planner, workOrderId)
  const stepId = (operation: string) => wo.operations.find((o) => o.operation === operation)!.id
  const electrodes = await lotsOf(request, operator, woNumber, 'ELECTRODE')
  expect(electrodes).toHaveLength(1)
  const electrode = electrodes[0]
  await inspectPass(request, qc, electrode)
  const [calBobbin] = await emptyCarriers(request, operator, 'BB', 1)
  await runOperation(request, operator, 'CP01', stepId('CAL'), [electrode], [
    { carrierCode: calBobbin, lane: null, goodQty: 990, rejectQty: 10 },
  ])
  await inspectPass(request, qc, electrode)
  const cores = await emptyCarriers(request, operator, 'PC', 2)
  await runOperation(request, operator, 'SL01', stepId('SLIT'), [electrode], [
    ...cores.map((core, index) => ({ carrierCode: core, lane: index + 1, goodQty: 120, rejectQty: 0 })),
    ...[3, 4, 5, 6, 7, 8].map((lane) => ({ carrierCode: null, lane, goodQty: 0, rejectQty: 120 })),
  ])
  const pancakes = await pancakesOf(request, operator, woNumber)
  expect(pancakes).toHaveLength(2)
  await inspectPass(request, qc, pancakes[0])
  const failed = pancakes[1]
  expect(await inspectFail(request, qc, failed, 'Burr height')).toBe('SL-BURR')

  //    QC releases the failed pancake with a reason (Quality, On hold, Disposition): it finishes and counts as good,
  //    so the work order completes at 2 / 2.
  await loginAs(page, 'QC')
  await page.getByRole('tab', { name: 'On hold' }).click()
  await page.getByRole('row', { name: new RegExp(failed) }).getByRole('button', { name: 'Disposition' }).click()
  const disposition = page.getByRole('dialog')
  await disposition.getByRole('radio', { name: 'Release' }).check()
  await disposition.getByLabel('Reason').fill('Burr re-measured within the customer tolerance')
  await disposition.getByRole('button', { name: 'Release lot' }).click()
  await expect(page.getByText(`${failed} released`)).toBeVisible()
  await nav(page, 'Work Orders').click()
  await main.getByRole('link', { name: woNumber, exact: true }).click()
  // Earlier runs' completed orders are on the list page too: assert only once this order's page is open.
  await expect(page).toHaveURL(new RegExp(`/work-orders/${workOrderId}$`))
  await expect(title).toHaveText(woNumber)
  await expect(main.getByText('COMPLETED', { exact: true })).toBeVisible()
  await expect(main.getByText('2 / 2', { exact: true })).toBeVisible()

  // 8. Trace a pancake back to its materials: lot detail, Genealogy tab, Backward. (screenshot)
  await main.getByRole('link', { name: pancakes[0], exact: true }).click()
  await page.getByRole('tab', { name: 'Genealogy' }).click()
  await page.getByRole('button', { name: 'Backward' }).click()
  const genealogy = page.getByRole('tabpanel')
  await expect(genealogy.locator('.react-flow__node')).toHaveCount(8)
  await expect(genealogy.getByText(foil, { exact: true })).toBeVisible()
  await expect(genealogy.getByText(slurry, { exact: true })).toBeVisible()
  await snap(page, '05-genealogy.png')

  // 9. Admin injects a fault on CT01: the dashboard shows CT01 DOWN with an active CRITICAL alarm. (screenshot)
  await ensureIdle(request, admin, 'CT01')
  await loginAs(page, 'Admin')
  await nav(page, 'Equipment').click()
  await main.getByRole('link', { name: 'CT01', exact: true }).click()
  await page.getByRole('button', { name: 'Inject fault' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Simulate fault' }).click()
  await expect(page.getByText('Fault requested on CT01')).toBeVisible()
  await nav(page, 'Dashboard').click()
  await expect(main.getByRole('link', { name: /^CT01\b/ })).toContainText('DOWN', { timeout: 30_000 })
  const activeAlarm = main.getByRole('listitem').filter({ hasText: 'CRITICAL' }).filter({ hasText: 'CT01' })
  await expect(activeAlarm).toBeVisible()
  await snap(page, '06-alarm.png', { alarmToastExpected: true })

  // 10. CT01's parameter trends over the last 15 minutes. (screenshot)
  await nav(page, 'Equipment').click()
  await main.getByRole('link', { name: 'CT01', exact: true }).click()
  await page.getByRole('tab', { name: 'Trends' }).click()
  await page.getByLabel('Range').selectOption('15')
  await expect(main.getByRole('figure')).toHaveCount(3)
  await snap(page, '07-equipment-trends.png')
})
