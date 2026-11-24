import styled from '@emotion/styled'
import { MutedText, SectionTitle } from '../../styles/components/texts.styles'

export const LandingSectionHead = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  max-width: 720px;
  margin: 0 auto 56px;
  text-align: center;
`

export const LandingSectionTitle = styled(SectionTitle)`
  max-width: 720px;
  margin: 0;
  font-size: var(--fs-display-md);
  font-weight: 700;
  line-height: 1.15;
  letter-spacing: -0.03em;
  text-align: center;
  color: var(--color-text-primary);

  @media (max-width: 600px) {
    font-size: var(--fs-26);
  }
`

export const LandingLead = styled(MutedText)`
  max-width: 680px;
  margin: 12px auto 0;
  text-align: center;
  font-size: var(--fs-18);
  line-height: 1.6;
  color: var(--color-text-secondary);
  opacity: 0.9;

  @media (max-width: 600px) {
    font-size: var(--fs-15);
  }
`
