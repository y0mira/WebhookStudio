import { expect,test } from '@playwright/test';
test('capture, filter, compare, and replay requests',async({page})=>{
 const slug=`phase-two-${Date.now()}`;await page.goto('/');await page.getByLabel('Name').fill('Phase two acceptance');await page.getByLabel('Slug').fill(slug);await page.getByRole('button',{name:'Create endpoint'}).click();await expect(page.getByText('Connected')).toBeVisible();
 await page.request.post(`/hooks/${slug}/orders?case=one`,{data:{orderId:1,state:'new'}});await page.request.post(`/hooks/${slug}/orders?case=two`,{data:{orderId:2,state:'paid'}});
 await expect(page.getByText('/orders?case=two')).toBeVisible();await page.getByLabel('HTTP method').selectOption('POST');await expect(page).toHaveURL(/method=POST/);
 const compare=page.getByRole('button',{name:'Add to comparison'});await compare.first().click();await compare.first().click();await expect(page.getByRole('heading',{name:'Request comparison'})).toBeVisible();await expect(page.getByText('changed').first()).toBeVisible();
 await page.getByRole('button',{name:'Close comparison'}).click();await page.getByText('/orders?case=two').click();await page.getByLabel('Target URL').fill(`http://localhost:5080/hooks/${slug}/replayed`);await page.getByRole('button',{name:'Replay',exact:true}).click();await expect(page.getByText(/Received HTTP 200/)).toBeVisible();
 for(const width of [375,768,1024,1440]){await page.setViewportSize({width,height:850});await page.waitForTimeout(350);await page.screenshot({path:`test-results/layout-${width}.png`,fullPage:true});const viewport=await page.evaluate(()=>[document.documentElement.scrollWidth,document.documentElement.clientWidth]);expect(viewport,`page overflow at ${width}px`).toEqual([width,width])}
});
